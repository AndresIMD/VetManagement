namespace SPVetClinic.Data.Models.Booking;

/// <summary>
/// Modelo que encapsula todos los datos del formulario de reserva
/// Se utiliza para gestionar el estado a través de múltiples componentes
/// </summary>
public class BookingFormData
{
    /// <summary>Especialista seleccionado</summary>
    public Specialist? SelectedSpecialist { get; set; }

    /// <summary>Franja horaria seleccionada</summary>
    public TimeSlot? SelectedTimeSlot { get; set; }

    /// <summary>Datos del propietario de la mascota</summary>
    public string OwnerName { get; set; } = "";
    public string OwnerEmail { get; set; } = "";
    public string OwnerPhone { get; set; } = "";

    /// <summary>Datos de la mascota</summary>
    public string PetName { get; set; } = "";
    public string PetSpecies { get; set; } = "";
    public string? PetBreed { get; set; }
    public string? PetAge { get; set; }

    /// <summary>Síntomas y notas</summary>
    public string? Symptoms { get; set; }
    public string? AdditionalNotes { get; set; }

    /// <summary>
    /// Convierte los datos del formulario a un objeto Reservation
    /// </summary>
    public Reservation ToReservation()
    {
        if (SelectedSpecialist == null || SelectedTimeSlot == null)
            throw new InvalidOperationException("Especialista y franja horaria son requeridos");

        return new Reservation
        {
            SpecialistId = SelectedSpecialist.Id,
            TimeSlotId = SelectedTimeSlot.Id,
            OwnerName = OwnerName,
            OwnerEmail = OwnerEmail,
            OwnerPhone = OwnerPhone,
            PetName = PetName,
            PetSpecies = PetSpecies,
            PetBreed = PetBreed,
            PetAge = PetAge,
            Symptoms = Symptoms,
            AdditionalNotes = AdditionalNotes,
            TotalPrice = SelectedSpecialist.ConsultationPrice,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reinicia todos los datos del formulario
    /// </summary>
    public void Reset()
    {
        SelectedSpecialist = null;
        SelectedTimeSlot = null;
        OwnerName = "";
        OwnerEmail = "";
        OwnerPhone = "";
        PetName = "";
        PetSpecies = "";
        PetBreed = null;
        PetAge = null;
        Symptoms = null;
        AdditionalNotes = null;
    }

    /// <summary>
    /// Valida que los datos esenciales estén completos
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(OwnerName) &&
               !string.IsNullOrWhiteSpace(OwnerEmail) &&
               !string.IsNullOrWhiteSpace(OwnerPhone) &&
               !string.IsNullOrWhiteSpace(PetName) &&
               !string.IsNullOrWhiteSpace(PetSpecies) &&
               SelectedSpecialist != null &&
               SelectedTimeSlot != null;
    }
}
