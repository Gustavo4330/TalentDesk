using Microsoft.Data.Sqlite;

namespace TalentDesk.Repositories
{
    public record Empresa(int Id, string Nome);

    // Empresas clientes. O nome é único (sem diferenciar maiúsculas).
    public class EmpresaRepository
    {
        private static SqliteConnection AbrirConexao()
        {
            BancoDeDados.GarantirInicializado();

            var conexao = Conexao.CriarConexao();
            conexao.Open();
            return conexao;
        }

        // Usa a empresa com este nome ou cria uma nova (convite de administrador)
        public int ObterOuCriar(string nome)
        {
            nome = nome.Trim();

            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                INSERT INTO empresas (nome, criado_em) VALUES (@nome, @agora)
                ON CONFLICT (nome) DO NOTHING;
                SELECT id FROM empresas WHERE nome = @nome;", conexao);
            comando.Parameters.AddWithValue("@nome", nome);
            comando.Parameters.AddWithValue("@agora", BancoDeDados.FormatarDataHora(DateTime.Now));

            return Convert.ToInt32(comando.ExecuteScalar());
        }

        public Empresa? ObterPorId(int id)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand("SELECT id, nome FROM empresas WHERE id = @id;", conexao);
            comando.Parameters.AddWithValue("@id", id);

            using var leitor = comando.ExecuteReader();
            return leitor.Read() ? new Empresa(leitor.GetInt32(0), leitor.GetString(1)) : null;
        }

        public List<Empresa> Listar()
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand("SELECT id, nome FROM empresas ORDER BY sem_acento(nome);", conexao);

            var empresas = new List<Empresa>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read()) empresas.Add(new Empresa(leitor.GetInt32(0), leitor.GetString(1)));
            return empresas;
        }
    }
}
