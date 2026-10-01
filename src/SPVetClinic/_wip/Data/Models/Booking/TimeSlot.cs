namespace SPVetClinic.Data.Models.Booking;

/// <summary>
/// Modelo que representa una franja horaria disponible para una reserva
/// </summary>
public class TimeSlot
{
    /// <summary>Identificador único de la franja horaria</summary>
    public int Id { get; set; }

    /// <summary>ID del especialista que tiene disponible esta hora</summary>
    public int SpecialistId { get; set; }

    /// <summary>Fecha de la consulta</summary>
    public DateTime Date { get; set; }

    /// <summary>Hora de inicio (formato HH:mm)</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Hora de fin (formato HH:mm)</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>Indica si la franja está disponible para reservar</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Reserva que ocupa esta franja (si está reservada)</summary>
    public int? ReservationId { get; set; }

    /// <summary>Referencia de navegación al especialista</summary>
    public Specialist? Specialist { get; set; }

    /// <summary>Propiedad calculada para obtener la hora formateada (HH:mm)</summary>
    public string FormattedTime
    {
        get => StartTime.ToString(@"hh\:mm");
    }

    /// <summary>Propiedad calculada para obtener el estado legible</summary>
    public string Status
    {
        get => IsAvailable ? "Disponible" : "Ocupada";
    }
}
