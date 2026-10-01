namespace SPVetClinic.Data.Models.Booking;

/// <summary>
/// Estados posibles de una reserva
/// </summary>
public enum ReservationStatus
{
    /// <summary>Reserva creada, pendiente de pago</summary>
    Pending,

    /// <summary>Pago en proceso en WebPay</summary>
    PaymentProcessing,

    /// <summary>Reserva confirmada, pago completado</summary>
    Confirmed,

    /// <summary>Reserva cancelada</summary>
    Cancelled,

    /// <summary>Pago fallido</summary>
    PaymentFailed
}

/// <summary>
/// Modelo que representa una reserva de consulta con especialista
/// </summary>
public class Reservation
{
    /// <summary>Identificador único de la reserva</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>ID del especialista reservado</summary>
    public int SpecialistId { get; set; }

    /// <summary>ID de la franja horaria reservada</summary>
    public int TimeSlotId { get; set; }

    /// <summary>Estado actual de la reserva</summary>
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    /// <summary>Nombre del propietario de la mascota</summary>
    public string OwnerName { get; set; } = "";

    /// <summary>Email del propietario</summary>
    public string OwnerEmail { get; set; } = "";

    /// <summary>Teléfono/WhatsApp del propietario (formato: +56XXXXXXXXX)</summary>
    public string OwnerPhone { get; set; } = "";

    /// <summary>Nombre de la mascota</summary>
    public string PetName { get; set; } = "";

    /// <summary>Especie de la mascota (Perro, Gato, etc.)</summary>
    public string PetSpecies { get; set; } = "";

    /// <summary>Raza de la mascota</summary>
    public string? PetBreed { get; set; }

    /// <summary>Edad de la mascota</summary>
    public string? PetAge { get; set; }

    /// <summary>Síntomas o motivo de la consulta</summary>
    public string? Symptoms { get; set; }

    /// <summary>Notas adicionales</summary>
    public string? AdditionalNotes { get; set; }

    /// <summary>Precio total de la reserva (CLP)</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Número de transacción WebPay (si pago completado)</summary>
    public string? WebPayTransactionId { get; set; }

    /// <summary>Orden de compra WebPay</summary>
    public string? WebPayOrderId { get; set; }

    /// <summary>Fecha y hora de creación de la reserva</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora de confirmación (cuando se completa el pago)</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Referencia de navegación al especialista</summary>
    public Specialist? Specialist { get; set; }

    /// <summary>Referencia de navegación a la franja horaria</summary>
    public TimeSlot? TimeSlot { get; set; }
}
