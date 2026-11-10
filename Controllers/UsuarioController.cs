using Microsoft.AspNetCore.Mvc;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly UsuarioRepository _usuarioRepository;

        public UsuarioController()
        {
            _usuarioRepository = new UsuarioRepository();
        }

        // O formulário de criar conta fica na tela estilizada /Curriculo/Login
        [HttpGet]
        public IActionResult Cadastro()
        {
            return RedirectToAction("Login", "Curriculo");
        }

        // ============ ENTRAR / SAIR ============

        [HttpGet]
        public IActionResult Entrar(string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new EntrarViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Entrar(EntrarViewModel model, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = _usuarioRepository.ObterPorEmail(model.Email);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(model.Senha, usuario.Senha))
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return View(model);
            }

            string perfil = _usuarioRepository.ObterPerfil(usuario.Id);
            SessaoUsuario.Entrar(HttpContext.Session, usuario.Id, usuario.Nome, perfil, usuario.FotoUrl);

            TempData["Mensagem"] = $"Bem-vindo(a), {usuario.Nome}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Curriculo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Sair()
        {
            SessaoUsuario.Sair(HttpContext.Session);
            TempData["Mensagem"] = "Você saiu da sua conta.";
            return RedirectToAction("Index", "Curriculo");
        }

        // ============ TELAS JÁ EXISTENTES ============

        [HttpGet]
        public IActionResult Login()
        {
            return View("~/Views/Curriculo/Login.cshtml", new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (model.DataNascimento?.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(model.DataNascimento), "A data de nascimento não pode ser futura.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/Curriculo/Login.cshtml", model);
            }

            TempData["MensagemSucesso"] = $"Bem-vindo(a), {model.Nome}! Seu acesso foi validado.";
            return RedirectToAction("Index", "Curriculo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cadastro(Usuario usuario)
        {
            Console.WriteLine("====== CADASTRO FOI CHAMADO ======");

            if (usuario.DataNascimento?.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(usuario.DataNascimento), "A data de nascimento não pode ser futura.");
            }

            if (!ModelState.IsValid)
            {
                return TelaDeCadastro(usuario);
            }

            try
            {
                // Verifica se o email já está cadastrado
                if (_usuarioRepository.EmailExiste(usuario.Email))
                {
                    ModelState.AddModelError(
                        "Email",
                        "Este email já está cadastrado."
                    );

                    return TelaDeCadastro(usuario);
                }

                Console.WriteLine("====== VOU CADASTRAR NO BANCO ======");

                // Realiza o cadastro
                int usuarioId =
                    _usuarioRepository.Cadastrar(usuario);

                Console.WriteLine("====== USUARIO CADASTRADO: " + usuarioId + " ======");

                // Cadastro realizado com sucesso
                TempData["MensagemSucesso"] = "Cadastro realizado com sucesso. Faça login para continuar.";
                return RedirectToAction(nameof(Entrar));
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                // Dois envios ao mesmo tempo (ex.: clique duplo): o banco barra o e-mail repetido
                ModelState.AddModelError("Email", "Este email já está cadastrado.");
                return TelaDeCadastro(usuario);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Erro ao cadastrar usuário: " + ex.Message
                );

                ModelState.AddModelError(
                    "",
                    "Ocorreu um erro ao realizar o cadastro. Tente novamente."
                );

                return TelaDeCadastro(usuario);
            }
        }

        // Volta para a tela estilizada de criar conta mostrando os erros.
        // Os nomes dos campos são os mesmos, então as mensagens aparecem em cada campo.
        private IActionResult TelaDeCadastro(Usuario usuario)
        {
            var model = new LoginViewModel
            {
                Nome = usuario.Nome,
                Email = usuario.Email,
                Whatsapp = usuario.Whatsapp,
                DataNascimento = usuario.DataNascimento
            };

            return View("~/Views/Curriculo/Login.cshtml", model);
        }
    }
}
