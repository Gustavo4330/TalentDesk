namespace TalentDesk.Models
{
    // Link de uso único para criar uma conta de administrador
    public class Convite
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? EmpresaNome { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime ExpiraEm { get; set; }
        public DateTime? UsadoEm { get; set; }

        public bool Usado => UsadoEm.HasValue;
        public bool Expirado => !Usado && ExpiraEm <= DateTime.Now;
        public bool Valido => !Usado && !Expirado;

        public string Situacao => Usado ? "Usado" : Expirado ? "Expirado" : "Aguardando";
    }
}
