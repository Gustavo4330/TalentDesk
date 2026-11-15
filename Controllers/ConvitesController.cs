using Microsoft.AspNetCore.Mvc;
using TalentDesk.Filters;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    // Equipe Talent Desk: gera os links de convite para os clientes criarem a conta de administrador.
    // (Futuramente o link será liberado automaticamente após o pagamento.)
    [Acesso(NivelAcesso.Superadmin)]
    public class ConvitesController : Controller
    {
        private readonly ConviteRepository _conviteRepository = new();
        private readonly EmpresaRepository _empresaRepository = new();

        [HttpGet]
        public IActionResult Index()
        {
            return View(MontarTela(new NovoConviteForm()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Criar([Bind(Prefix = "Form")] NovoConviteForm form)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(Index), MontarTela(form));
            }

            int empresaId = _empresaRepository.ObterOuCriar(form.Empresa);
            string token = _conviteRepository.Criar(form.Email, empresaId, SessaoUsuario.Id(HttpContext.Session));

            // O token não fica salvo em lugar nenhum: o link só aparece nesta resposta
            var tela = MontarTela(new NovoConviteForm());
            tela.LinkGerado = Url.Action("Convite", "Usuario", new { token }, Request.Scheme);
            tela.EmailGerado = $"{form.Email.Trim()} ({form.Empresa.Trim()})";
            return View(nameof(Index), tela);
        }

        private ConvitesViewModel MontarTela(NovoConviteForm form) => new()
        {
            Form = form,
            Convites = _conviteRepository.ListarRecentes(),
            Empresas = _empresaRepository.Listar().Select(e => e.Nome).ToList()
        };
    }
}
