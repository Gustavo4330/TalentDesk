using Microsoft.AspNetCore.Mvc;
using TalentDesk.Filters;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    // Tela inicial do candidato
    [Acesso(NivelAcesso.Candidato)]
    public class InicioController : Controller
    {
        private readonly InicioCandidatoService _servico = new(new VagaRepository(), new CurriculoRepository());

        [HttpGet]
        public IActionResult Index()
        {
            var sessao = HttpContext.Session;
            return View(_servico.Montar(SessaoUsuario.Id(sessao)!.Value, SessaoUsuario.PrimeiroNome(sessao)));
        }
    }
}
