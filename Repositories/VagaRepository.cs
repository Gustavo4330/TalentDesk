using Microsoft.Data.Sqlite;
using TalentDesk.Models;

namespace TalentDesk.Repositories
{
    // Quem está gerenciando: a vaga pertence à empresa (quando o admin tem empresa)
    // ou a quem a criou (contas sem empresa, como o superadmin).
    public readonly record struct Gestor(int UsuarioId, int? EmpresaId);

    // Vagas, candidaturas e vagas salvas. As regras de dono ficam no próprio SQL:
    // um UPDATE de quem não gerencia a vaga simplesmente não altera nenhuma linha.
    public class VagaRepository
    {
        private const string ColunasVaga = @"
            v.id, v.titulo, v.empresa, v.area, v.cidade, v.modalidade, v.tipo_contrato,
            v.faixa_salarial, v.descricao, v.requisitos, v.status, v.criado_por, v.criado_em, v.empresa_id,
            (SELECT COUNT(*) FROM candidaturas c WHERE c.vaga_id = v.id) AS total_inscritos";

        // Dados do candidato logado sobre a vaga (inscrição, status dela e se salvou)
        private const string ColunasCandidato = @"
            ca.status AS status_candidatura,
            COALESCE(ca.atualizado_em, ca.criado_em) AS candidatura_atualizada_em,
            EXISTS (SELECT 1 FROM vagas_salvas s WHERE s.vaga_id = v.id AND s.usuario_id = @usuario_id) AS salva";

        private const string JuncaoCandidato =
            "LEFT JOIN candidaturas ca ON ca.vaga_id = v.id AND ca.curriculo_id = @curriculo_id";

        private const string CondicaoGestor =
            "(v.criado_por = @gestor_usuario OR (v.empresa_id IS NOT NULL AND v.empresa_id = @gestor_empresa))";

        private static SqliteConnection AbrirConexao()
        {
            BancoDeDados.GarantirInicializado();

            var conexao = Conexao.CriarConexao();
            conexao.Open();
            return conexao;
        }

        // ================= CONSULTAS =================

        public Vaga? ObterPorId(int id, int? curriculoId = null, int? usuarioId = null)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand($@"
                SELECT {ColunasVaga}, {ColunasCandidato}
                FROM vagas v {JuncaoCandidato}
                WHERE v.id = @id;", conexao);
            comando.Parameters.AddWithValue("@id", id);
            ParametrosCandidato(comando, curriculoId, usuarioId);

            using var leitor = comando.ExecuteReader();
            return leitor.Read() ? Ler(leitor) : null;
        }

        // Vagas da empresa do gestor (abertas primeiro)
        public List<Vaga> ListarDoGestor(Gestor gestor)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand($@"
                SELECT {ColunasVaga}, {ColunasCandidato}
                FROM vagas v {JuncaoCandidato}
                WHERE {CondicaoGestor}
                ORDER BY v.status = 'Encerrada', v.criado_em DESC;", conexao);
            ParametrosGestor(comando, gestor);
            ParametrosCandidato(comando, null, null);

            return LerLista(comando);
        }

        // Vagas abertas para o candidato, com filtros
        public List<Vaga> ListarAbertas(FiltroVaga filtro, int? curriculoId, int? usuarioId)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand { Connection = conexao };

            var condicoes = new List<string> { "v.status = 'Aberta'" };
            if (filtro.Busca != null)
            {
                condicoes.Add(@"(sem_acento(v.titulo) LIKE @busca OR sem_acento(v.empresa) LIKE @busca
                                 OR sem_acento(v.descricao) LIKE @busca OR sem_acento(v.requisitos) LIKE @busca)");
                comando.Parameters.AddWithValue("@busca", $"%{Conexao.SemAcento(filtro.Busca)}%");
            }
            if (filtro.Area != null)
            {
                condicoes.Add("v.area = @area");
                comando.Parameters.AddWithValue("@area", filtro.Area);
            }
            if (filtro.Modalidade != null)
            {
                condicoes.Add("v.modalidade = @modalidade");
                comando.Parameters.AddWithValue("@modalidade", filtro.Modalidade);
            }
            ParametrosCandidato(comando, curriculoId, usuarioId);

            comando.CommandText = $@"
                SELECT {ColunasVaga}, {ColunasCandidato}
                FROM vagas v {JuncaoCandidato}
                WHERE {string.Join(" AND ", condicoes)}
                ORDER BY v.criado_em DESC;";

            return LerLista(comando);
        }

        // Vagas em que o currículo está inscrito (inclui as encerradas), mais recentes primeiro
        public List<Vaga> ListarCandidaturas(int curriculoId, int usuarioId)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand($@"
                SELECT {ColunasVaga}, {ColunasCandidato}
                FROM vagas v INNER JOIN candidaturas ca ON ca.vaga_id = v.id AND ca.curriculo_id = @curriculo_id
                ORDER BY COALESCE(ca.atualizado_em, ca.criado_em) DESC;", conexao);
            ParametrosCandidato(comando, curriculoId, usuarioId);

            return LerLista(comando);
        }

        // Vagas que o candidato salvou (inclui as encerradas, para ele saber o que aconteceu)
        public List<Vaga> ListarSalvas(int usuarioId, int? curriculoId)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand($@"
                SELECT {ColunasVaga}, {ColunasCandidato}
                FROM vagas v
                INNER JOIN vagas_salvas vs ON vs.vaga_id = v.id AND vs.usuario_id = @usuario_id
                {JuncaoCandidato}
                ORDER BY vs.criado_em DESC;", conexao);
            ParametrosCandidato(comando, curriculoId, usuarioId);

            return LerLista(comando);
        }

        public int ContarCandidaturas(int curriculoId) =>
            Contar("SELECT COUNT(*) FROM candidaturas WHERE curriculo_id = @id;", curriculoId);

        public int ContarSalvas(int usuarioId) =>
            Contar("SELECT COUNT(*) FROM vagas_salvas WHERE usuario_id = @id;", usuarioId);

        // Vagas abertas publicadas nos últimos dias
        public int ContarAbertasRecentes(int dias)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(
                "SELECT COUNT(*) FROM vagas WHERE status = 'Aberta' AND criado_em >= @desde;", conexao);
            comando.Parameters.AddWithValue("@desde", BancoDeDados.FormatarDataHora(DateTime.Now.AddDays(-dias)));
            return Convert.ToInt32(comando.ExecuteScalar());
        }

        // Áreas das vagas abertas (para o filtro)
        public List<string> ListarAreasAbertas()
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(
                "SELECT DISTINCT area FROM vagas WHERE status = 'Aberta' ORDER BY sem_acento(area);", conexao);

            var areas = new List<string>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read()) areas.Add(leitor.GetString(0));
            return areas;
        }

        // ================= GRAVAÇÃO (empresa) =================

        public int Inserir(Vaga vaga, Gestor gestor)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                INSERT INTO vagas (titulo, empresa, area, cidade, modalidade, tipo_contrato, faixa_salarial,
                                   descricao, requisitos, status, criado_por, empresa_id, criado_em)
                VALUES (@titulo, @empresa, @area, @cidade, @modalidade, @tipo_contrato, @faixa_salarial,
                        @descricao, @requisitos, 'Aberta', @gestor_usuario, @gestor_empresa, @agora);
                SELECT last_insert_rowid();", conexao);
            ParametrosVaga(comando, vaga);
            ParametrosGestor(comando, gestor);
            return Convert.ToInt32(comando.ExecuteScalar());
        }

        // Só altera se a vaga for da empresa (ou de quem criou)
        public bool Atualizar(int id, Vaga vaga, Gestor gestor)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand($@"
                UPDATE vagas AS v SET
                    titulo = @titulo, empresa = @empresa, area = @area, cidade = @cidade,
                    modalidade = @modalidade, tipo_contrato = @tipo_contrato, faixa_salarial = @faixa_salarial,
                    descricao = @descricao, requisitos = @requisitos, atualizado_em = @agora
                WHERE v.id = @id AND {CondicaoGestor};", conexao);
            ParametrosVaga(comando, vaga);
            ParametrosGestor(comando, gestor);
            comando.Parameters.AddWithValue("@id", id);
            return comando.ExecuteNonQuery() > 0;
        }

        public bool AlterarStatus(int id, string status, Gestor gestor)
        {
            if (status is not (StatusVaga.Aberta or StatusVaga.Encerrada)) return false;

            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand($@"
                UPDATE vagas AS v SET status = @status, atualizado_em = @agora
                WHERE v.id = @id AND {CondicaoGestor};", conexao);
            comando.Parameters.AddWithValue("@status", status);
            comando.Parameters.AddWithValue("@agora", Agora());
            comando.Parameters.AddWithValue("@id", id);
            ParametrosGestor(comando, gestor);
            return comando.ExecuteNonQuery() > 0;
        }

        // ================= INSCRITOS E STATUS (empresa) =================

        // Inscritos de uma vaga, com o status da candidatura (não o status geral do currículo)
        public List<Inscrito> ListarInscritos(int vagaId)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                SELECT ca.id, c.id, c.nome, c.cargo, c.cidade, c.anos_experiencia, ca.status, ca.criado_em
                FROM candidaturas ca
                INNER JOIN curriculos c ON c.id = ca.curriculo_id
                WHERE ca.vaga_id = @vaga_id
                ORDER BY ca.criado_em DESC;", conexao);
            comando.Parameters.AddWithValue("@vaga_id", vagaId);

            var inscritos = new List<Inscrito>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read())
            {
                inscritos.Add(new Inscrito
                {
                    CandidaturaId = leitor.GetInt32(0),
                    CurriculoId = leitor.GetInt32(1),
                    Nome = leitor.GetString(2),
                    Cargo = leitor.GetString(3),
                    Cidade = leitor.GetString(4),
                    AnosExperiencia = leitor.GetInt32(5),
                    Status = leitor.GetString(6),
                    InscritoEm = BancoDeDados.LerData(leitor[7]) ?? DateTime.MinValue
                });
            }
            return inscritos;
        }

        // Muda o status de uma candidatura, só se a vaga for do gestor; grava no histórico.
        // Devolve o id da vaga (para voltar à tela dela) ou null se não pôde alterar.
        public int? AlterarStatusCandidatura(int candidaturaId, string status, Gestor gestor)
        {
            if (!StatusCurriculo.EhValido(status)) return null;

            using var conexao = AbrirConexao();
            using var transacao = conexao.BeginTransaction();

            using var buscar = new SqliteCommand($@"
                SELECT ca.vaga_id, ca.status FROM candidaturas ca
                INNER JOIN vagas v ON v.id = ca.vaga_id
                WHERE ca.id = @id AND {CondicaoGestor};", conexao, transacao);
            buscar.Parameters.AddWithValue("@id", candidaturaId);
            ParametrosGestor(buscar, gestor);

            int vagaId;
            string statusAtual;
            using (var leitor = buscar.ExecuteReader())
            {
                if (!leitor.Read()) return null;
                vagaId = leitor.GetInt32(0);
                statusAtual = leitor.GetString(1);
            }

            if (statusAtual != status)
            {
                string agora = Agora();
                using var atualizar = new SqliteCommand(@"
                    UPDATE candidaturas SET status = @status, atualizado_em = @agora WHERE id = @id;
                    INSERT INTO candidatura_historico (candidatura_id, status, criado_em) VALUES (@id, @status, @agora);",
                    conexao, transacao);
                atualizar.Parameters.AddWithValue("@status", status);
                atualizar.Parameters.AddWithValue("@agora", agora);
                atualizar.Parameters.AddWithValue("@id", candidaturaId);
                atualizar.ExecuteNonQuery();
            }

            transacao.Commit();
            return vagaId;
        }

        // ================= CANDIDATURAS (candidato) =================

        // Inscreve o currículo só se a vaga estiver aberta; repetir não duplica
        public bool Candidatar(int vagaId, int curriculoId)
        {
            using var conexao = AbrirConexao();
            using var transacao = conexao.BeginTransaction();

            string agora = Agora();
            using var inserir = new SqliteCommand(@"
                INSERT OR IGNORE INTO candidaturas (vaga_id, curriculo_id, criado_em, status)
                SELECT id, @curriculo_id, @agora, 'Novo' FROM vagas WHERE id = @vaga_id AND status = 'Aberta';",
                conexao, transacao);
            inserir.Parameters.AddWithValue("@vaga_id", vagaId);
            inserir.Parameters.AddWithValue("@curriculo_id", curriculoId);
            inserir.Parameters.AddWithValue("@agora", agora);

            if (inserir.ExecuteNonQuery() == 0)
            {
                return false;
            }

            using var historico = new SqliteCommand(@"
                INSERT INTO candidatura_historico (candidatura_id, status, criado_em)
                VALUES (last_insert_rowid(), 'Novo', @agora);", conexao, transacao);
            historico.Parameters.AddWithValue("@agora", agora);
            historico.ExecuteNonQuery();

            transacao.Commit();
            return true;
        }

        public bool CancelarCandidatura(int vagaId, int curriculoId)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(
                "DELETE FROM candidaturas WHERE vaga_id = @vaga_id AND curriculo_id = @curriculo_id;", conexao);
            comando.Parameters.AddWithValue("@vaga_id", vagaId);
            comando.Parameters.AddWithValue("@curriculo_id", curriculoId);
            return comando.ExecuteNonQuery() > 0;
        }

        // Linha do tempo de todas as candidaturas do currículo (mais recente primeiro)
        public List<EventoCandidatura> ListarHistorico(int curriculoId, int limite = 50)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(@"
                SELECT v.id, v.titulo, v.empresa, h.status, h.criado_em
                FROM candidatura_historico h
                INNER JOIN candidaturas ca ON ca.id = h.candidatura_id
                INNER JOIN vagas v ON v.id = ca.vaga_id
                WHERE ca.curriculo_id = @curriculo_id
                ORDER BY h.criado_em DESC, h.id DESC
                LIMIT @limite;", conexao);
            comando.Parameters.AddWithValue("@curriculo_id", curriculoId);
            comando.Parameters.AddWithValue("@limite", limite);

            var eventos = new List<EventoCandidatura>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read())
            {
                eventos.Add(new EventoCandidatura
                {
                    VagaId = leitor.GetInt32(0),
                    VagaTitulo = leitor.GetString(1),
                    Empresa = leitor.GetString(2),
                    Status = leitor.GetString(3),
                    Data = BancoDeDados.LerData(leitor[4]) ?? DateTime.MinValue
                });
            }
            return eventos;
        }

        // ================= VAGAS SALVAS (candidato) =================

        // Salva ou remove dos salvos; devolve true se ficou salva
        public bool AlternarSalva(int usuarioId, int vagaId)
        {
            using var conexao = AbrirConexao();
            using var remover = new SqliteCommand(
                "DELETE FROM vagas_salvas WHERE usuario_id = @usuario_id AND vaga_id = @vaga_id;", conexao);
            remover.Parameters.AddWithValue("@usuario_id", usuarioId);
            remover.Parameters.AddWithValue("@vaga_id", vagaId);
            if (remover.ExecuteNonQuery() > 0) return false;

            using var salvar = new SqliteCommand(@"
                INSERT OR IGNORE INTO vagas_salvas (usuario_id, vaga_id, criado_em)
                SELECT @usuario_id, id, @agora FROM vagas WHERE id = @vaga_id;", conexao);
            salvar.Parameters.AddWithValue("@usuario_id", usuarioId);
            salvar.Parameters.AddWithValue("@vaga_id", vagaId);
            salvar.Parameters.AddWithValue("@agora", Agora());
            return salvar.ExecuteNonQuery() > 0;
        }

        // ================= AUXILIARES =================

        private static string Agora() => BancoDeDados.FormatarDataHora(DateTime.Now);

        private static int Contar(string sql, int id)
        {
            using var conexao = AbrirConexao();
            using var comando = new SqliteCommand(sql, conexao);
            comando.Parameters.AddWithValue("@id", id);
            return Convert.ToInt32(comando.ExecuteScalar());
        }

        private static void ParametrosCandidato(SqliteCommand comando, int? curriculoId, int? usuarioId)
        {
            comando.Parameters.AddWithValue("@curriculo_id", (object?)curriculoId ?? DBNull.Value);
            comando.Parameters.AddWithValue("@usuario_id", (object?)usuarioId ?? DBNull.Value);
        }

        private static void ParametrosGestor(SqliteCommand comando, Gestor gestor)
        {
            comando.Parameters.AddWithValue("@gestor_usuario", gestor.UsuarioId);
            comando.Parameters.AddWithValue("@gestor_empresa", (object?)gestor.EmpresaId ?? DBNull.Value);
        }

        private static void ParametrosVaga(SqliteCommand comando, Vaga vaga)
        {
            comando.Parameters.AddWithValue("@titulo", vaga.Titulo);
            comando.Parameters.AddWithValue("@empresa", vaga.Empresa);
            comando.Parameters.AddWithValue("@area", vaga.Area);
            comando.Parameters.AddWithValue("@cidade", vaga.Cidade);
            comando.Parameters.AddWithValue("@modalidade", vaga.Modalidade);
            comando.Parameters.AddWithValue("@tipo_contrato", vaga.TipoContrato);
            comando.Parameters.AddWithValue("@faixa_salarial", (object?)vaga.FaixaSalarial ?? DBNull.Value);
            comando.Parameters.AddWithValue("@descricao", vaga.Descricao);
            comando.Parameters.AddWithValue("@requisitos", (object?)vaga.Requisitos ?? DBNull.Value);
            comando.Parameters.AddWithValue("@agora", Agora());
        }

        private static List<Vaga> LerLista(SqliteCommand comando)
        {
            var vagas = new List<Vaga>();
            using var leitor = comando.ExecuteReader();
            while (leitor.Read()) vagas.Add(Ler(leitor));
            return vagas;
        }

        private static Vaga Ler(SqliteDataReader leitor) => new()
        {
            Id = leitor.GetInt32(leitor.GetOrdinal("id")),
            Titulo = leitor.GetString(leitor.GetOrdinal("titulo")),
            Empresa = leitor.GetString(leitor.GetOrdinal("empresa")),
            Area = leitor.GetString(leitor.GetOrdinal("area")),
            Cidade = leitor.GetString(leitor.GetOrdinal("cidade")),
            Modalidade = leitor.GetString(leitor.GetOrdinal("modalidade")),
            TipoContrato = leitor.GetString(leitor.GetOrdinal("tipo_contrato")),
            FaixaSalarial = leitor["faixa_salarial"] as string,
            Descricao = leitor.GetString(leitor.GetOrdinal("descricao")),
            Requisitos = leitor["requisitos"] as string,
            Status = leitor.GetString(leitor.GetOrdinal("status")),
            CriadoPor = leitor.GetInt32(leitor.GetOrdinal("criado_por")),
            CriadoEm = BancoDeDados.LerData(leitor["criado_em"]) ?? DateTime.MinValue,
            EmpresaId = leitor["empresa_id"] is long empresaId ? (int)empresaId : null,
            TotalInscritos = Convert.ToInt32(leitor["total_inscritos"]),
            StatusCandidatura = leitor["status_candidatura"] as string,
            CandidaturaAtualizadaEm = BancoDeDados.LerData(leitor["candidatura_atualizada_em"]),
            Salva = Convert.ToInt64(leitor["salva"]) == 1
        };
    }
}
