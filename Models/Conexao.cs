using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TalentDesk
{
    public class Conexao
    {
        // O banco é um arquivo só: %LOCALAPPDATA%\TalentDesk\talentdesk.db
        // (ex.: C:\Users\SeuUsuario\AppData\Local\TalentDesk\talentdesk.db).
        // Fica fora da pasta do projeto para não ser versionado nem sincronizado
        // por serviços de nuvem enquanto está em uso.
        public static string CaminhoDoBanco { get; private set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TalentDesk", "talentdesk.db");

        // "Foreign Keys = True" faz o SQLite executar "PRAGMA foreign_keys = ON;" toda
        // vez que a conexão é aberta. Sem isso o ON DELETE CASCADE não funciona.
        private static string connectionString = MontarConnectionString(CaminhoDoBanco);

        // Permite usar outro arquivo (configuração "Banco:Caminho"). Os testes automatizados
        // usam isso para trabalhar num banco temporário, sem tocar nos dados do dia a dia.
        public static void DefinirCaminho(string? caminho)
        {
            if (string.IsNullOrWhiteSpace(caminho)) return;

            CaminhoDoBanco = Path.GetFullPath(caminho);
            connectionString = MontarConnectionString(CaminhoDoBanco);
        }

        private static string MontarConnectionString(string caminho) => new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            ForeignKeys = true
        }.ToString();

        public static SqliteConnection CriarConexao()
        {
            // Cria a pasta se não existir. O arquivo .db o próprio SQLite cria ao abrir.
            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoDoBanco)!);

            var conexao = new SqliteConnection(connectionString);

            // Função usada nas buscas e ordenações: sem_acento('São Paulo') = 'sao paulo'
            conexao.CreateFunction("sem_acento", (string? texto) => SemAcento(texto));

            return conexao;
        }

        // Tira os acentos e passa para minúsculas, para a busca não diferenciar
        // "JOÃO", "joão" e "joao" (o LIKE do SQLite só ignora maiúsculas em letras sem acento)
        public static string? SemAcento(string? texto)
        {
            if (texto == null) return null;

            var resultado = new StringBuilder(texto.Length);
            foreach (char letra in texto.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(letra);
                }
            }

            return resultado.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }
    }
}
