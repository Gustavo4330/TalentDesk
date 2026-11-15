using Microsoft.AspNetCore.Mvc;
using TalentDesk.Filters;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    // Vagas: a empresa publica e gerencia as vagas dela; o candidato consulta, salva e se candidata.
    // Todas as telas exigem login (ControleAcessoFilter).
    public class VagasController : Controller
    {
        private readonly VagaRepository _vagaRepository = new();
        private readonly CurriculoRepository _curriculoRepository = new();

        // ============ LISTA ============

        [HttpGet]
        public IActionResult Index([FromQuery] FiltroVaga filtro)
        {
            filtro.Normalizar();

            if (EhEmpresa)
            {
                return View(new VagasIndexViewModel
                {
                    EhEmpresa = true,
                    Vagas = _vagaRepository.ListarDoGestor(GestorLogado)
                });
            }

            int? curriculoId = CurriculoDoCandidato();
            var viewModel = new VagasIndexViewModel
            {
                Filtro = filtro,
                Areas = _vagaRepository.ListarAreasAbertas(),
                TemCurriculo = curriculoId.HasValue,
                TotalCandidaturas = curriculoId.HasValue ? _vagaRepository.ContarCandidaturas(curriculoId.Value) : 0,
                TotalSalvas = _vagaRepository.ContarSalvas(UsuarioLogadoId)
            };

            viewModel.Vagas = filtro.Aba switch
            {
                FiltroVaga.AbaCandidaturas => curriculoId.HasValue
                    ? _vagaRepository.ListarCandidaturas(curriculoId.Value, UsuarioLogadoId)
                    : new List<Vaga>(),
                FiltroVaga.AbaSalvas => _vagaRepository.ListarSalvas(UsuarioLogadoId, curriculoId),
                _ => _vagaRepository.ListarAbertas(filtro, curriculoId, UsuarioLogadoId)
            };

            return View(viewModel);
        }

        // ============ DETALHES ============

        [HttpGet]
        public IActionResult Detalhes(int id)
        {
            int? curriculoId = EhEmpresa ? null : CurriculoDoCandidato();
            var vaga = _vagaRepository.ObterPorId(id, curriculoId, EhEmpresa ? null : UsuarioLogadoId);
            if (vaga == null)
            {
                return NotFound();
            }

            bool ehDono = EhEmpresa && Gerencia(vaga);

            // A empresa só vê as vagas dela (e os inscritos delas)
            if (EhEmpresa && !ehDono)
            {
                return AcessoNegado();
            }

            // O candidato vê vagas abertas e as encerradas em que se inscreveu ou que salvou
            if (!EhEmpresa && !vaga.Aberta && !vaga.JaCandidatado && !vaga.Salva)
            {
                return NotFound();
            }

            return View(new VagaDetalhesViewModel
            {
                Vaga = vaga,
                EhDono = ehDono,
                TemCurriculo = curriculoId.HasValue,
                Inscritos = ehDono ? _vagaRepository.ListarInscritos(id) : new List<Inscrito>()
            });
        }

        // ============ CADASTRO E EDIÇÃO (empresa, só as vagas dela) ============

        [HttpGet]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Nova()
        {
            // O nome da empresa já vem preenchido para quem tem empresa
            var form = new VagaFormViewModel { Empresa = SessaoUsuario.EmpresaNome(HttpContext.Session) ?? string.Empty };
            PreencherSugestoes(form);
            return View("Formulario", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Nova(VagaFormViewModel form)
        {
            form.Id = null;
            if (!ModelState.IsValid)
            {
                PreencherSugestoes(form);
                return View("Formulario", form);
            }

            int id = _vagaRepository.Inserir(form.ParaVaga(), GestorLogado);
            TempData["Mensagem"] = "Vaga publicada.";
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        [HttpGet]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Editar(int id)
        {
            var vaga = _vagaRepository.ObterPorId(id);
            if (vaga == null) return NotFound();
            if (!Gerencia(vaga)) return AcessoNegado();

            var form = VagaFormViewModel.DeVaga(vaga);
            PreencherSugestoes(form);
            return View("Formulario", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Editar(int id, VagaFormViewModel form)
        {
            var vaga = _vagaRepository.ObterPorId(id);
            if (vaga == null) return NotFound();
            if (!Gerencia(vaga)) return AcessoNegado();

            form.Id = id;
            if (!ModelState.IsValid)
            {
                PreencherSugestoes(form);
                return View("Formulario", form);
            }

            _vagaRepository.Atualizar(id, form.ParaVaga(), GestorLogado);
            TempData["Mensagem"] = "Vaga atualizada.";
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        // Encerrar para de receber candidaturas; reabrir volta a receber
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult AlterarStatus(int id, string status)
        {
            if (!_vagaRepository.AlterarStatus(id, status, GestorLogado))
            {
                return AcessoNegado();
            }

            TempData["Mensagem"] = status == StatusVaga.Encerrada
                ? "Vaga encerrada: ela não recebe mais candidaturas."
                : "Vaga reaberta.";
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        // Move um inscrito de etapa no quadro da vaga (status desta candidatura apenas)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult AlterarStatusCandidatura(int id, string status)
        {
            int? vagaId = _vagaRepository.AlterarStatusCandidatura(id, status, GestorLogado);
            if (vagaId == null)
            {
                return AcessoNegado();
            }

            TempData["Mensagem"] = $"Candidato movido para \"{status}\".";
            return RedirectToAction(nameof(Detalhes), null, new { id = vagaId.Value }, "quadro");
        }

        // ============ CANDIDATURA E SALVOS (candidato) ============

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult Candidatar(int id)
        {
            int? curriculoId = CurriculoDoCandidato();
            if (curriculoId == null)
            {
                TempData["Mensagem"] = "Cadastre seu currículo antes de se candidatar.";
                return RedirectToAction("Cadastrar", "Curriculo");
            }

            TempData["Mensagem"] = _vagaRepository.Candidatar(id, curriculoId.Value)
                ? "Candidatura enviada! A empresa já pode ver seu currículo."
                : "Não foi possível se candidatar: a vaga foi encerrada ou você já está inscrito.";
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult CancelarCandidatura(int id)
        {
            int? curriculoId = CurriculoDoCandidato();
            if (curriculoId.HasValue && _vagaRepository.CancelarCandidatura(id, curriculoId.Value))
            {
                TempData["Mensagem"] = "Candidatura cancelada.";
            }

            return RedirectToAction(nameof(Index), new { aba = FiltroVaga.AbaCandidaturas });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult AlternarSalva(int id, string? returnUrl)
        {
            bool salva = _vagaRepository.AlternarSalva(UsuarioLogadoId, id);
            TempData["Mensagem"] = salva ? "Vaga salva." : "Vaga removida das salvas.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        // ============ AUXILIARES ============

        private int UsuarioLogadoId => SessaoUsuario.Id(HttpContext.Session)!.Value;

        private Gestor GestorLogado => new(UsuarioLogadoId, SessaoUsuario.EmpresaId(HttpContext.Session));

        private bool EhEmpresa => Perfis.EhEmpresa(SessaoUsuario.Perfil(HttpContext.Session));

        // A vaga é da empresa do usuário ou foi criada por ele (contas sem empresa)
        private bool Gerencia(Vaga vaga)
        {
            int? empresaId = SessaoUsuario.EmpresaId(HttpContext.Session);
            return vaga.CriadoPor == UsuarioLogadoId
                || (vaga.EmpresaId.HasValue && vaga.EmpresaId == empresaId);
        }

        private int? CurriculoDoCandidato() =>
            Perfis.EhCandidato(SessaoUsuario.Perfil(HttpContext.Session))
                ? _curriculoRepository.ObterIdPorUsuario(UsuarioLogadoId)
                : null;

        private IActionResult AcessoNegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("AcessoNegado");
        }

        private void PreencherSugestoes(VagaFormViewModel form)
        {
            form.SugestoesAreas = _curriculoRepository.ListarAreas();
            form.SugestoesCidades = _curriculoRepository.ListarCidades();
        }
    }
}
