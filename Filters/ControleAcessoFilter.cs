using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TalentDesk.Models;
using TalentDesk.Services;

namespace TalentDesk.Filters
{
    // Tela aberta para visitantes (login, criar conta, convite)
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class PublicoAttribute : Attribute
    {
    }

    // Restringe a tela a um grupo de perfis. Sem este atributo, basta estar logado.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AcessoAttribute : Attribute
    {
        public NivelAcesso Nivel { get; }

        public AcessoAttribute(NivelAcesso nivel)
        {
            Nivel = nivel;
        }
    }

    // Filtro global: toda tela exige login, a não ser que tenha [Publico].
    // Assim uma tela nova nasce protegida, mesmo que alguém esqueça o atributo.
    public class ControleAcessoFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var metadados = context.ActionDescriptor.EndpointMetadata;
            if (metadados.OfType<PublicoAttribute>().Any())
            {
                return;
            }

            var http = context.HttpContext;
            var sessao = http.Session;

            if (!SessaoUsuario.EstaLogado(sessao))
            {
                // Depois do login volta para a tela pedida (só em GET; POST perderia os dados)
                string? retorno = HttpMethods.IsGet(http.Request.Method)
                    ? http.Request.Path + http.Request.QueryString
                    : null;
                context.Result = new RedirectToActionResult("Entrar", "Usuario", new { returnUrl = retorno });
                return;
            }

            // O atributo da ação vale mais que o do controller
            var acesso = metadados.OfType<AcessoAttribute>().LastOrDefault();
            if (acesso == null)
            {
                return;
            }

            string? perfil = SessaoUsuario.Perfil(sessao);
            if (Permite(acesso.Nivel, perfil))
            {
                return;
            }

            // Candidato que abre uma tela da empresa vai para a tela inicial dele
            if (Perfis.EhCandidato(perfil) && HttpMethods.IsGet(http.Request.Method))
            {
                context.Result = new RedirectToActionResult("Index", "Inicio", null);
                return;
            }

            context.Result = new ViewResult { ViewName = "AcessoNegado", StatusCode = StatusCodes.Status403Forbidden };
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        public static bool Permite(NivelAcesso nivel, string? perfil) => nivel switch
        {
            NivelAcesso.Logado => perfil != null,
            NivelAcesso.Empresa => Perfis.EhEmpresa(perfil),
            NivelAcesso.Candidato => Perfis.EhCandidato(perfil),
            NivelAcesso.Superadmin => Perfis.EhSuperadmin(perfil),
            _ => false
        };
    }
}
