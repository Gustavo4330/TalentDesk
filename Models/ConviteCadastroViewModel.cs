using System.ComponentModel.DataAnnotations;

namespace TalentDesk.Models
{
    // Tela em que o cliente convidado cria a conta de administrador
    public class ConviteCadastroViewModel
    {
        // O e-mail vem do convite e não pode ser trocado no formulário
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(120, ErrorMessage = "O nome pode ter até 120 caracteres.")]
        [Display(Name = "Nome completo")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Crie uma senha.")]
        [MinLength(8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repita a senha.")]
        [Compare(nameof(Senha), ErrorMessage = "As senhas não conferem.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar senha")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }

    // Formulário da tela de convites (superadmin)
    public class NovoConviteForm
    {
        [Required(ErrorMessage = "Informe o e-mail do cliente.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Display(Name = "E-mail do cliente")]
        public string Email { get; set; } = string.Empty;
    }

    public class ConvitesViewModel
    {
        public NovoConviteForm Form { get; set; } = new();
        public List<Convite> Convites { get; set; } = new();

        // Link recém-gerado (só aparece uma vez, logo após criar)
        public string? LinkGerado { get; set; }
        public string? EmailGerado { get; set; }
    }
}
