using System.Data;
using Microsoft.Data.Sqlite;
using TalentDesk.Models;
using BCrypt.Net;

namespace TalentDesk.Repositories
{
    public class UsuarioRepository
    {
        // Abre a conexão garantindo antes que as tabelas existem
        private static SqliteConnection AbrirConexao()
        {
            BancoDeDados.GarantirInicializado();

            var conexao = Conexao.CriarConexao();
            conexao.Open();
            return conexao;
        }

        public bool EmailExiste(string email)
        {
            using var conexao = AbrirConexao();

            string sql = @"
                SELECT COUNT(*)
                FROM usuarios
                WHERE email = @email;
            ";

            using var comando = new SqliteCommand(sql, conexao);

            comando.Parameters.AddWithValue("@email", email);

            int quantidade = Convert.ToInt32(comando.ExecuteScalar());

            return quantidade > 0;
        }

        public Usuario? ObterPorEmail(string email)
        {
            using var conexao = AbrirConexao();

            string sql = @"
                SELECT id, nome, email, senha_hash, whatsapp, data_nascimento, foto_url, empresa_id
                FROM usuarios
                WHERE email = @email;
            ";

            using var comando = new SqliteCommand(sql, conexao);
            comando.Parameters.AddWithValue("@email", email);

            using var leitor = comando.ExecuteReader();
            if (!leitor.Read())
            {
                return null;
            }

            return new Usuario
            {
                Id = leitor.GetInt32("id"),
                Nome = leitor.GetString("nome"),
                Email = leitor.GetString("email"),
                Senha = leitor["senha_hash"] as string ?? string.Empty,
                Whatsapp = leitor["whatsapp"] as string,
                // A data vem como texto (ou nula) e é convertida com segurança
                DataNascimento = BancoDeDados.LerData(leitor["data_nascimento"]),
                FotoUrl = leitor["foto_url"] as string,
                EmpresaId = leitor["empresa_id"] is long empresaId ? (int)empresaId : null
            };
        }

        // Nome do perfil do usuário (Candidato, Recrutador ou Administrador)
        public string ObterPerfil(int usuarioId)
        {
            using var conexao = AbrirConexao();

            string sql = @"
                SELECT p.nome
                FROM usuario_perfil up
                INNER JOIN perfis p ON p.id = up.perfil_id
                WHERE up.usuario_id = @usuario_id
                ORDER BY p.id DESC
                LIMIT 1;
            ";

            using var comando = new SqliteCommand(sql, conexao);
            comando.Parameters.AddWithValue("@usuario_id", usuarioId);

            return comando.ExecuteScalar() as string ?? "Usuário";
        }

        // ================= CONFIGURAÇÕES DA CONTA =================

        public Usuario? ObterPorId(int id)
        {
            using var conexao = AbrirConexao();

            string sql = @"
                SELECT id, nome, email, senha_hash, whatsapp, data_nascimento, foto_url, data_cadastro
                FROM usuarios
                WHERE id = @id;
            ";

            using var comando = new SqliteCommand(sql, conexao);
            comando.Parameters.AddWithValue("@id", id);

            using var leitor = comando.ExecuteReader();
            if (!leitor.Read())
            {
                return null;
            }

            return new Usuario
            {
                Id = leitor.GetInt32("id"),
                Nome = leitor.GetString("nome"),
                Email = leitor.GetString("email"),
                Senha = leitor["senha_hash"] as string ?? string.Empty,
                Whatsapp = leitor["whatsapp"] as string,
                DataNascimento = BancoDeDados.LerData(leitor["data_nascimento"]),
                FotoUrl = leitor["foto_url"] as string,
                DataCadastro = BancoDeDados.LerData(leitor["data_cadastro"])
            };
        }

        // O e-mail já pertence a outra conta? (usado ao editar o perfil)
        public bool EmailExisteEmOutraConta(string email, int usuarioId)
        {
            using var conexao = AbrirConexao();

            using var comando = new SqliteCommand(
                "SELECT COUNT(*) FROM usuarios WHERE email = @email AND id <> @id;", conexao);
            comando.Parameters.AddWithValue("@email", email);
            comando.Parameters.AddWithValue("@id", usuarioId);

            return Convert.ToInt32(comando.ExecuteScalar()) > 0;
        }

        // Atualiza nome, e-mail, WhatsApp, data de nascimento e foto (não mexe na senha)
        public bool AtualizarPerfil(Usuario usuario)
        {
            using var conexao = AbrirConexao();

            using var comando = new SqliteCommand(@"
                UPDATE usuarios SET
                    nome = @nome,
                    email = @email,
                    whatsapp = @whatsapp,
                    data_nascimento = @data_nascimento,
                    foto_url = @foto_url
                WHERE id = @id;", conexao);

            comando.Parameters.AddWithValue("@nome", usuario.Nome);
            comando.Parameters.AddWithValue("@email", usuario.Email);
            comando.Parameters.AddWithValue("@whatsapp", (object?)usuario.Whatsapp ?? DBNull.Value);
            comando.Parameters.AddWithValue("@data_nascimento",
                usuario.DataNascimento.HasValue
                    ? (object)BancoDeDados.FormatarData(usuario.DataNascimento.Value)
                    : DBNull.Value);
            comando.Parameters.AddWithValue("@foto_url", (object?)usuario.FotoUrl ?? DBNull.Value);
            comando.Parameters.AddWithValue("@id", usuario.Id);

            return comando.ExecuteNonQuery() > 0;
        }

        // Grava o hash BCrypt da nova senha
        public bool AlterarSenha(int usuarioId, string novaSenha)
        {
            using var conexao = AbrirConexao();

            using var comando = new SqliteCommand(
                "UPDATE usuarios SET senha_hash = @senha_hash WHERE id = @id;", conexao);
            comando.Parameters.AddWithValue("@senha_hash", BCrypt.Net.BCrypt.HashPassword(novaSenha));
            comando.Parameters.AddWithValue("@id", usuarioId);

            return comando.ExecuteNonQuery() > 0;
        }

        // Apaga a conta. O vínculo com o perfil (usuario_perfil) sai junto (ON DELETE CASCADE).
        public bool Excluir(int usuarioId)
        {
            using var conexao = AbrirConexao();

            using var comando = new SqliteCommand("DELETE FROM usuarios WHERE id = @id;", conexao);
            comando.Parameters.AddWithValue("@id", usuarioId);

            return comando.ExecuteNonQuery() > 0;
        }

        // Cadastro aberto do site: sempre cria conta de candidato
        public int Cadastrar(Usuario usuario) => Cadastrar(usuario, Perfis.Candidato, conviteId: null);

        // Conta de administrador criada pelo link de convite. Tudo na mesma transação:
        // se o convite já foi usado ou expirou, nenhuma conta é criada.
        public int CadastrarComConvite(Usuario usuario, int conviteId) =>
            Cadastrar(usuario, Perfis.Administrador, conviteId);

        private int Cadastrar(Usuario usuario, string perfil, int? conviteId)
        {
            using var conexao = AbrirConexao();
            using var transacao = conexao.BeginTransaction();

            try
            {
                string agora = BancoDeDados.FormatarDataHora(DateTime.Now);

                // Marca o convite como usado antes de criar a conta (evita dois cadastros com o mesmo link)
                if (conviteId.HasValue)
                {
                    using var usarConvite = new SqliteCommand(@"
                        UPDATE convites SET usado_em = @agora
                        WHERE id = @id AND usado_em IS NULL AND expira_em > @agora;", conexao, transacao);
                    usarConvite.Parameters.AddWithValue("@id", conviteId.Value);
                    usarConvite.Parameters.AddWithValue("@agora", agora);

                    if (usarConvite.ExecuteNonQuery() == 0)
                    {
                        throw new ConviteInvalidoException();
                    }
                }

                // A conta criada por convite entra na empresa do convite
                using var comandoUsuario = new SqliteCommand(@"
                    INSERT INTO usuarios (nome, email, senha_hash, whatsapp, data_nascimento, foto_url, empresa_id)
                    VALUES (@nome, @email, @senha_hash, @whatsapp, @data_nascimento, @foto_url,
                            (SELECT empresa_id FROM convites WHERE id = @convite_id));
                    SELECT last_insert_rowid();", conexao, transacao);
                comandoUsuario.Parameters.AddWithValue("@convite_id", (object?)conviteId ?? DBNull.Value);

                comandoUsuario.Parameters.AddWithValue("@nome", usuario.Nome);
                comandoUsuario.Parameters.AddWithValue("@email", usuario.Email);
                comandoUsuario.Parameters.AddWithValue("@senha_hash", BCrypt.Net.BCrypt.HashPassword(usuario.Senha));
                comandoUsuario.Parameters.AddWithValue("@whatsapp", (object?)usuario.Whatsapp ?? DBNull.Value);
                // Data de nascimento gravada como texto (ex.: "1990-01-31")
                comandoUsuario.Parameters.AddWithValue("@data_nascimento",
                    usuario.DataNascimento.HasValue
                        ? BancoDeDados.FormatarData(usuario.DataNascimento.Value)
                        : DBNull.Value);
                comandoUsuario.Parameters.AddWithValue("@foto_url", (object?)usuario.FotoUrl ?? DBNull.Value);

                // last_insert_rowid() devolve long; o id cabe em int
                int usuarioId = Convert.ToInt32(comandoUsuario.ExecuteScalar());

                InserirPerfil(conexao, transacao, usuarioId, perfil);

                if (conviteId.HasValue)
                {
                    using var registrar = new SqliteCommand(
                        "UPDATE convites SET usado_por = @usuario_id WHERE id = @id;", conexao, transacao);
                    registrar.Parameters.AddWithValue("@usuario_id", usuarioId);
                    registrar.Parameters.AddWithValue("@id", conviteId.Value);
                    registrar.ExecuteNonQuery();
                }

                transacao.Commit();
                return usuarioId;
            }
            catch
            {
                // Desfaz tudo caso alguma etapa dê erro
                transacao.Rollback();
                throw;
            }
        }

        private static void InserirPerfil(SqliteConnection conexao, SqliteTransaction transacao, int usuarioId, string perfil)
        {
            using var comando = new SqliteCommand(@"
                INSERT INTO usuario_perfil (usuario_id, perfil_id)
                SELECT @usuario_id, id FROM perfis WHERE nome = @perfil;", conexao, transacao);
            comando.Parameters.AddWithValue("@usuario_id", usuarioId);
            comando.Parameters.AddWithValue("@perfil", perfil);

            if (comando.ExecuteNonQuery() == 0)
            {
                throw new InvalidOperationException($"O perfil {perfil} não foi encontrado.");
            }
        }
    }

    // O convite já foi usado ou expirou
    public class ConviteInvalidoException : Exception
    {
        public ConviteInvalidoException() : base("Este convite já foi usado ou expirou.") { }
    }
}
