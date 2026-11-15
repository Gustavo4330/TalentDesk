using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using TalentDesk.Models;

namespace TalentDesk.Repositories
{
    // Convites de administrador. O token só existe no link enviado ao cliente;
    // o banco guarda apenas o hash SHA-256 dele.
    public class ConviteRepository
    {
        public static readonly TimeSpan Validade = TimeSpan.FromDays(7);

        private static SqliteConnection AbrirConexao()
        {
            BancoDeDados.GarantirInicializado();

            var conexao = Conexao.CriarConexao();
            conexao.Open();
            return conexao;
        }

        // Cria o convite e devolve o token (só aparece agora, para montar o link)
        public string Criar(string email, int empresaId, int? criadoPor)
        {
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var agora = DateTime.Now;

            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                INSERT INTO convites (email, token_hash, empresa_id, criado_por, criado_em, expira_em)
                VALUES (@email, @token_hash, @empresa_id, @criado_por, @criado_em, @expira_em);", conexao);
            comando.Parameters.AddWithValue("@empresa_id", empresaId);

            comando.Parameters.AddWithValue("@email", email.Trim());
            comando.Parameters.AddWithValue("@token_hash", Hash(token));
            comando.Parameters.AddWithValue("@criado_por", (object?)criadoPor ?? DBNull.Value);
            comando.Parameters.AddWithValue("@criado_em", BancoDeDados.FormatarDataHora(agora));
            comando.Parameters.AddWithValue("@expira_em", BancoDeDados.FormatarDataHora(agora.Add(Validade)));
            comando.ExecuteNonQuery();

            return token;
        }

        // Convite ainda utilizável para este token (null se não existe, expirou ou já foi usado)
        public Convite? ObterValido(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;

            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                SELECT c.id, c.email, c.criado_em, c.expira_em, c.usado_em, e.nome
                FROM convites c LEFT JOIN empresas e ON e.id = c.empresa_id
                WHERE c.token_hash = @token_hash;", conexao);
            comando.Parameters.AddWithValue("@token_hash", Hash(token));

            using var leitor = comando.ExecuteReader();
            if (!leitor.Read()) return null;

            var convite = Ler(leitor);
            return convite.Valido ? convite : null;
        }

        public List<Convite> ListarRecentes(int limite = 20)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                SELECT c.id, c.email, c.criado_em, c.expira_em, c.usado_em, e.nome
                FROM convites c LEFT JOIN empresas e ON e.id = c.empresa_id
                ORDER BY c.id DESC
                LIMIT @limite;", conexao);
            comando.Parameters.AddWithValue("@limite", limite);

            var itens = new List<Convite>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read())
            {
                itens.Add(Ler(leitor));
            }
            return itens;
        }

        private static Convite Ler(SqliteDataReader leitor) => new()
        {
            Id = leitor.GetInt32(0),
            Email = leitor.GetString(1),
            CriadoEm = BancoDeDados.LerData(leitor[2]) ?? DateTime.MinValue,
            ExpiraEm = BancoDeDados.LerData(leitor[3]) ?? DateTime.MinValue,
            UsadoEm = BancoDeDados.LerData(leitor[4]),
            EmpresaNome = leitor[5] as string
        };

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
