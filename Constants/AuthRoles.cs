
namespace ClinicaAPI.Constants;

/// <summary>Nombres canónicos de roles — usar siempre estas constantes.</summary>
public static class RoleNames
{
    public const string Admin         = "Admin";
    public const string Doctor        = "Doctor";
    public const string Recepcionista = "Recepcionista";
    public const string Paciente      = "Paciente";
}

/// <summary>Strings para [Authorize(Roles = ...)] — incluyen el nombre legacy "Administrador".</summary>
public static class AuthRoles
{
    public const string SoloAdmin       = "Admin,Administrador";
    public const string AdminYRecepcion = "Admin,Administrador,Recepcionista";
    public const string TodosStaff      = "Admin,Administrador,Doctor,Recepcionista";
}