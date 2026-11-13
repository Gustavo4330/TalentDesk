namespace TalentDesk.Models
{
    // Perfis de acesso (mesmos nomes da tabela "perfis")
    public static class Perfis
    {
        public const string Candidato = "Candidato";
        public const string Recrutador = "Recrutador";
        public const string Administrador = "Administrador";

        // Equipe do Talent Desk: gera os convites dos administradores
        public const string Superadmin = "Superadmin";

        // Empresa = quem avalia currículos (lê e muda o status, nunca edita o perfil)
        public static bool EhEmpresa(string? perfil) =>
            perfil is Recrutador or Administrador or Superadmin;

        public static bool EhCandidato(string? perfil) => perfil == Candidato;

        public static bool EhSuperadmin(string? perfil) => perfil == Superadmin;
    }

    // Quem pode abrir uma tela (usado com o atributo [Acesso])
    public enum NivelAcesso
    {
        Logado,
        Empresa,
        Candidato,
        Superadmin
    }
}
