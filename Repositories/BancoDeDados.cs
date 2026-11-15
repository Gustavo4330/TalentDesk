using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TalentDesk.Repositories
{
    // Cria o arquivo do banco e as tabelas (se ainda não existirem), cadastra os
    // perfis e os currículos de exemplo na primeira vez que o sistema roda.
    public static class BancoDeDados
    {
        private static readonly object _trava = new();
        private static bool _inicializado;

        // No SQLite as datas são guardadas como texto, sempre neste formato
        private const string FormatoDataHora = "yyyy-MM-dd HH:mm:ss";
        private const string FormatoData = "yyyy-MM-dd";

        private const string SqlCriarTabelas = @"
            CREATE TABLE IF NOT EXISTS curriculos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome TEXT NOT NULL,
                cargo TEXT NOT NULL,
                area TEXT NOT NULL,
                formacao TEXT NOT NULL,
                formacao_detalhada TEXT NULL,
                instituicao TEXT NULL,
                conclusao_formacao TEXT NULL,
                idade INTEGER NOT NULL,
                anos_experiencia INTEGER NOT NULL DEFAULT 0,
                foto_url TEXT NULL,
                email TEXT NOT NULL,
                telefone TEXT NULL,
                cidade TEXT NOT NULL,
                linkedin TEXT NULL,
                resumo TEXT NULL,
                status TEXT NOT NULL DEFAULT 'Novo',
                data_cadastro TEXT NOT NULL,
                data_atualizacao TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_curriculos_data ON curriculos (data_cadastro);
            CREATE INDEX IF NOT EXISTS idx_curriculos_status ON curriculos (status);

            CREATE TABLE IF NOT EXISTS curriculo_experiencias (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                curriculo_id INTEGER NOT NULL,
                cargo TEXT NOT NULL,
                empresa TEXT NOT NULL,
                periodo TEXT NULL,
                descricao TEXT NULL,
                ordem INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT fk_experiencias_curriculo FOREIGN KEY (curriculo_id)
                    REFERENCES curriculos (id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_experiencias_curriculo ON curriculo_experiencias (curriculo_id);

            CREATE TABLE IF NOT EXISTS curriculo_habilidades (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                curriculo_id INTEGER NOT NULL,
                nome TEXT NOT NULL,
                CONSTRAINT fk_habilidades_curriculo FOREIGN KEY (curriculo_id)
                    REFERENCES curriculos (id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_habilidades_curriculo ON curriculo_habilidades (curriculo_id);

            CREATE TABLE IF NOT EXISTS perfis (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome TEXT NOT NULL UNIQUE
            );

            -- COLLATE NOCASE: o e-mail não diferencia maiúsculas de minúsculas (como era no MySQL)
            CREATE TABLE IF NOT EXISTS usuarios (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE COLLATE NOCASE,
                senha_hash TEXT NOT NULL,
                whatsapp TEXT NULL,
                data_nascimento TEXT NULL,
                foto_url TEXT NULL,
                data_cadastro TEXT DEFAULT (datetime('now', 'localtime')),
                ativo INTEGER DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS usuario_perfil (
                usuario_id INTEGER NOT NULL,
                perfil_id INTEGER NOT NULL,
                PRIMARY KEY (usuario_id, perfil_id),
                CONSTRAINT fk_usuario_perfil_usuario FOREIGN KEY (usuario_id)
                    REFERENCES usuarios (id) ON DELETE CASCADE,
                CONSTRAINT fk_usuario_perfil_perfil FOREIGN KEY (perfil_id)
                    REFERENCES perfis (id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_usuario_perfil_perfil ON usuario_perfil (perfil_id);

            -- Perfis iniciais (OR IGNORE: não duplica se já existirem)
            INSERT OR IGNORE INTO perfis (id, nome) VALUES
                (1, 'Candidato'),
                (2, 'Recrutador'),
                (3, 'Administrador'),
                (4, 'Superadmin');

            -- Convites de administrador: só o hash do token fica no banco
            CREATE TABLE IF NOT EXISTS convites (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email TEXT NOT NULL COLLATE NOCASE,
                token_hash TEXT NOT NULL UNIQUE,
                criado_por INTEGER NULL REFERENCES usuarios (id) ON DELETE SET NULL,
                criado_em TEXT NOT NULL,
                expira_em TEXT NOT NULL,
                usado_em TEXT NULL,
                usado_por INTEGER NULL REFERENCES usuarios (id) ON DELETE SET NULL
            );

            -- Vagas publicadas pela empresa. Só quem criou edita e encerra (criado_por).
            CREATE TABLE IF NOT EXISTS vagas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                titulo TEXT NOT NULL,
                empresa TEXT NOT NULL,
                area TEXT NOT NULL,
                cidade TEXT NOT NULL,
                modalidade TEXT NOT NULL,
                tipo_contrato TEXT NOT NULL,
                faixa_salarial TEXT NULL,
                descricao TEXT NOT NULL,
                requisitos TEXT NULL,
                status TEXT NOT NULL DEFAULT 'Aberta',
                criado_por INTEGER NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
                criado_em TEXT NOT NULL,
                atualizado_em TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_vagas_criado_por ON vagas (criado_por);
            CREATE INDEX IF NOT EXISTS idx_vagas_status ON vagas (status);

            -- Candidato inscrito numa vaga (pelo currículo dele). Uma inscrição por vaga.
            CREATE TABLE IF NOT EXISTS candidaturas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                vaga_id INTEGER NOT NULL REFERENCES vagas (id) ON DELETE CASCADE,
                curriculo_id INTEGER NOT NULL REFERENCES curriculos (id) ON DELETE CASCADE,
                criado_em TEXT NOT NULL,
                UNIQUE (vaga_id, curriculo_id)
            );

            CREATE INDEX IF NOT EXISTS idx_candidaturas_curriculo ON candidaturas (curriculo_id);

            -- Empresas clientes: administradores da mesma empresa compartilham as vagas
            CREATE TABLE IF NOT EXISTS empresas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome TEXT NOT NULL UNIQUE COLLATE NOCASE,
                criado_em TEXT NOT NULL
            );

            -- Cada mudança de status de uma candidatura (linha do tempo do candidato)
            CREATE TABLE IF NOT EXISTS candidatura_historico (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                candidatura_id INTEGER NOT NULL REFERENCES candidaturas (id) ON DELETE CASCADE,
                status TEXT NOT NULL,
                criado_em TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_historico_candidatura ON candidatura_historico (candidatura_id);

            -- Vagas que o candidato guardou para decidir depois
            CREATE TABLE IF NOT EXISTS vagas_salvas (
                usuario_id INTEGER NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
                vaga_id INTEGER NOT NULL REFERENCES vagas (id) ON DELETE CASCADE,
                criado_em TEXT NOT NULL,
                PRIMARY KEY (usuario_id, vaga_id)
            );
        ";

        // Colunas acrescentadas depois da primeira versão: bancos antigos ganham a coluna
        // na inicialização (o SQLite não tem "ADD COLUMN IF NOT EXISTS").
        private static readonly (string Tabela, string Coluna, string Definicao)[] ColunasNovas =
        {
            // Dono do currículo; excluir a conta apaga o currículo junto
            ("curriculos", "usuario_id", "INTEGER NULL REFERENCES usuarios (id) ON DELETE CASCADE"),
            ("usuarios", "empresa_id", "INTEGER NULL REFERENCES empresas (id) ON DELETE SET NULL"),
            ("vagas", "empresa_id", "INTEGER NULL REFERENCES empresas (id) ON DELETE SET NULL"),
            ("convites", "empresa_id", "INTEGER NULL REFERENCES empresas (id) ON DELETE SET NULL"),
            // Status de cada candidatura (definido pela empresa dona da vaga)
            ("candidaturas", "status", "TEXT NOT NULL DEFAULT 'Novo'"),
            ("candidaturas", "atualizado_em", "TEXT NULL")
        };

        // Índices e ajustes que dependem das colunas novas
        private const string SqlDepoisDasColunas = @"
            -- Um candidato tem no máximo um currículo
            CREATE UNIQUE INDEX IF NOT EXISTS idx_curriculos_usuario
                ON curriculos (usuario_id) WHERE usuario_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS idx_usuarios_empresa ON usuarios (empresa_id);
            CREATE INDEX IF NOT EXISTS idx_vagas_empresa ON vagas (empresa_id);

            -- Candidaturas antigas ganham o primeiro passo da linha do tempo
            INSERT INTO candidatura_historico (candidatura_id, status, criado_em)
            SELECT c.id, c.status, c.criado_em FROM candidaturas c
            WHERE NOT EXISTS (SELECT 1 FROM candidatura_historico h WHERE h.candidatura_id = c.id);";

        private static void GarantirColuna(SqliteConnection conexao, string tabela, string coluna, string definicao)
        {
            using var existe = new SqliteCommand(
                "SELECT COUNT(*) FROM pragma_table_info(@tabela) WHERE name = @coluna;", conexao);
            existe.Parameters.AddWithValue("@tabela", tabela);
            existe.Parameters.AddWithValue("@coluna", coluna);
            if (Convert.ToInt64(existe.ExecuteScalar()) > 0) return;

            // Nomes vêm da lista fixa acima, nunca de entrada do usuário
            using var alterar = new SqliteCommand($"ALTER TABLE {tabela} ADD COLUMN {coluna} {definicao};", conexao);
            alterar.ExecuteNonQuery();
        }

        // Chamado no início do programa. Se der algum problema, só avisa no
        // console: a próxima tela que usar o banco tenta de novo.
        public static void TentarInicializar(ILogger logger)
        {
            try
            {
                GarantirInicializado();
                logger.LogInformation("Banco de dados pronto em {Caminho}.", Conexao.CaminhoDoBanco);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Não foi possível preparar o banco agora: {Mensagem}. " +
                                  "Verifique se o sistema pode gravar o arquivo {Caminho}.",
                                  ex.Message, Conexao.CaminhoDoBanco);
            }
        }

        public static void GarantirInicializado()
        {
            if (_inicializado) return;

            lock (_trava)
            {
                if (_inicializado) return;

                // Ao abrir, o SQLite cria o arquivo .db se ele ainda não existir
                using var conexao = Conexao.CriarConexao();
                conexao.Open();

                using (var comando = new SqliteCommand(SqlCriarTabelas, conexao))
                {
                    comando.ExecuteNonQuery();
                }

                foreach (var (tabela, coluna, definicao) in ColunasNovas)
                {
                    GarantirColuna(conexao, tabela, coluna, definicao);
                }

                using (var ajustes = new SqliteCommand(SqlDepoisDasColunas, conexao))
                {
                    ajustes.ExecuteNonQuery();
                }

                using (var contar = new SqliteCommand("SELECT COUNT(*) FROM curriculos;", conexao))
                {
                    long total = Convert.ToInt64(contar.ExecuteScalar());
                    if (total == 0)
                    {
                        var repositorio = new CurriculoRepository(inicializarBanco: false);
                        foreach (var curriculo in DadosIniciais.Curriculos())
                        {
                            repositorio.Inserir(curriculo);
                        }
                    }
                }

                _inicializado = true;
            }
        }

        // ================= DATAS =================

        // Data e hora para gravar no banco (ex.: "2026-10-11 14:30:00")
        public static string FormatarDataHora(DateTime data)
        {
            return data.ToString(FormatoDataHora, CultureInfo.InvariantCulture);
        }

        // Só a data, sem hora (ex.: "1990-01-31")
        public static string FormatarData(DateTime data)
        {
            return data.ToString(FormatoData, CultureInfo.InvariantCulture);
        }

        // Lê uma data gravada como texto. Devolve null se estiver vazia ou inválida.
        public static DateTime? LerData(object? valor)
        {
            if (valor is not string texto || string.IsNullOrWhiteSpace(texto))
            {
                return null;
            }

            string[] formatos = { FormatoDataHora, FormatoData };

            if (DateTime.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ||
                DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out data))
            {
                return data;
            }

            return null;
        }
    }
}
