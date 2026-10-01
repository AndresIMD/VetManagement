namespace SPVetClinic.Data.Models.Booking;

/// <summary>
/// Modelo que representa un especialista veterinario disponible para reservas
/// </summary>
public class Specialist
{
    /// <summary>Identificador único del especialista</summary>
    public int Id { get; set; }

    /// <summary>Nombre completo del especialista</summary>
    public string Name { get; set; } = "";

    /// <summary>Especialidad del veterinario (Neurología, Medicina Felina, etc.)</summary>
    public string Specialty { get; set; } = "";

    /// <summary>Descripción breve del especialista</summary>
    public string Description { get; set; } = "";

    /// <summary>Ícono asociado (brain, cat, flask, heart, stethoscope)</summary>
    public string Icon { get; set; } = "stethoscope";

    /// <summary>Color del avatar (teal, coral, purple)</summary>
    public string AvatarColor { get; set; } = "teal";

    /// <summary>Precio por consulta en CLP</summary>
    public decimal ConsultationPrice { get; set; }

    /// <summary>Duración de la consulta en minutos</summary>
    public int ConsultationDurationMinutes { get; set; } = 30;

    /// <summary>Indica si el especialista está activo para nuevas reservas</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Información de contacto/teléfono del especialista (opcional)</summary>
    public string? ContactInfo { get; set; }

    /// <summary>Horarios de atención disponibles (ej: "Martes a Viernes, 9AM-5PM")</summary>
    public string? AvailableSchedule { get; set; }
}
