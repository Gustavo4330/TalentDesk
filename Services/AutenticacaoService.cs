using Microsoft.AspNetCore.Http;
using TalentDesk.Models;
using TalentDesk.Repositories;

namespace TalentDesk.Services
{
    // Confere e-mail e senha e abre a sessão com o perfil do usuário.
    // Usado pelas duas telas de login (Usuario/Entrar e Curriculo/Login).
    public class AutenticacaoService
    {
        // E-mails da equipe Talent Desk que entram como Superadmin.
        // Vêm da configuração "Superadmin:Emails" (variável de ambiente ou user-secrets),
        // nunca do código, para nenhum endereço ficar versionado.
        private static HashSet<string> _superadmins = new(StringComparer.OrdinalIgnoreCase);

        public static void ConfigurarSuperadmins(IEnumerable<string>? emails)
        {
            _superadmins = new HashSet<string>(
                (emails ?? Enumerable.Empty<string>()).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        private readonly UsuarioRepository _usuarioRepository = new();

        // Devolve o usuário se a senha confere (e já deixa a sessão aberta); senão, null
        public Usuario? Entrar(ISession sessao, string email, string senha)
        {
            var usuario = _usuarioRepository.ObterPorEmail(email);
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(senha, usuario.Senha))
            {
                return null;
            }

            // Superadmin vale só enquanto o e-mail estiver na configuração: não é gravado no banco,
            // então tirar o e-mail da lista já revoga o acesso no próximo login
            string perfil = _superadmins.Contains(usuario.Email)
                ? Perfis.Superadmin
                : _usuarioRepository.ObterPerfil(usuario.Id);

            SessaoUsuario.Entrar(sessao, usuario.Id, usuario.Nome, perfil, usuario.FotoUrl);
            return usuario;
        }
    }
}
