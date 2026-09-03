using Microsoft.AspNetCore.Mvc;
using TalentDesk.Models;
using TalentDesk.Repositories;

namespace TalentDesk.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly UsuarioRepository _usuarioRepository;

        public UsuarioController()
        {
            _usuarioRepository = new UsuarioRepository();
        }

        [HttpGet]
        public IActionResult Cadastro()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View("~/Views/Curriculo/Login.cshtml", new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Curriculo/Login.cshtml", model);
            }

            TempData["MensagemSucesso"] = $"Bem-vindo(a), {model.Nome}! Seu acesso foi validado.";
            return RedirectToAction("Index", "Curriculo");
        }

        [HttpPost]
        public IActionResult Cadastro(Usuario usuario)
        {
            Console.WriteLine("====== CADASTRO FOI CHAMADO ======");

            if (!ModelState.IsValid)
            {
                return View(usuario);
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

                    return View(usuario);
                }

                Console.WriteLine("====== VOU CADASTRAR NO BANCO ======");

                // Realiza o cadastro
                int usuarioId =
                    _usuarioRepository.Cadastrar(usuario);

                Console.WriteLine("====== USUARIO CADASTRADO: " + usuarioId + " ======");

                // Cadastro realizado com sucesso
                TempData["MensagemSucesso"] = "Cadastro realizado com sucesso. Faça login para continuar.";
                return RedirectToAction("Login", "Curriculo");
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

                return View(usuario);
            }
        }
    }
}