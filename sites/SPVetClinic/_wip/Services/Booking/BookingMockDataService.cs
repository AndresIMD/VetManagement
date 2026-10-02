namespace SPVetClinic.Services.Booking;

using SPVetClinic.Data.Models.Booking;

/// <summary>
/// Servicio que proporciona datos mock de especialistas y horarios disponibles
/// Esta es una implementación temporal que será reemplazada por llamadas a API backend
/// </summary>
public class BookingMockDataService
{
    /// <summary>
    /// Obtiene la lista de especialistas mock
    /// </summary>
    public List<Specialist> GetMockSpecialists()
    {
        return new List<Specialist>
        {
            new Specialist
            {
                Id = 1,
                Name = "Dra. Catalina Soto",
                Specialty = "Neurología",
                Description = "Especialista en enfermedades del sistema nervioso. Diagnóstico de convulsiones, parálisis y comportamiento anormal.",
                Icon = "brain",
                AvatarColor = "teal",
                ConsultationPrice = 85000,
                ConsultationDurationMinutes = 45,
                IsActive = true,
                ContactInfo = "+56 9 6190 0401",
                AvailableSchedule = "Martes a Viernes, 10:00 AM - 5:00 PM"
            },
            new Specialist
            {
                Id = 2,
                Name = "Dr. Mauricio González",
                Specialty = "Medicina Felina",
                Description = "Experto en patologías felinas. Tratamiento de enfermedades gastrointestinales, diabetes y problemas renales en gatos.",
                Icon = "cat",
                AvatarColor = "coral",
                ConsultationPrice = 75000,
                ConsultationDurationMinutes = 30,
                IsActive = true,
                ContactInfo = "+56 9 6190 0401",
                AvailableSchedule = "Lunes a Jueves, 9:00 AM - 4:00 PM"
            },
            new Specialist
            {
                Id = 3,
                Name = "Dra. Patricia Ramírez",
                Specialty = "Endocrinología",
                Description = "Especialista en desórdenes endocrinos. Tratamiento de diabetes, hipertiroidismo y otros desórdenes hormonales.",
                Icon = "flask",
                AvatarColor = "purple",
                ConsultationPrice = 80000,
                ConsultationDurationMinutes = 40,
                IsActive = true,
                ContactInfo = "+56 9 6190 0401",
                AvailableSchedule = "Miércoles a Viernes, 11:00 AM - 6:00 PM"
            },
            new Specialist
            {
                Id = 4,
                Name = "Dr. Andrés Morales",
                Specialty = "Cardiología",
                Description = "Especialista en enfermedades cardiovasculares. Diagnóstico y tratamiento de soplos cardíacos, arritmias e insuficiencia cardíaca.",
                Icon = "heart",
                AvatarColor = "teal",
                ConsultationPrice = 90000,
                ConsultationDurationMinutes = 45,
                IsActive = true,
                ContactInfo = "+56 9 6190 0401",
                AvailableSchedule = "Lunes a Miércoles, 2:00 PM - 7:00 PM"
            }
        };
    }

    /// <summary>
    /// Obtiene franjas horarias mock para cada especialista
    /// </summary>
    public Dictionary<int, List<TimeSlot>> GetMockTimeSlots()
    {
        var timeSlots = new Dictionary<int, List<TimeSlot>>();

        // Generar 14 días de horas disponibles a partir de hoy
        var today = DateTime.Today;

        // Especialista 1: Neurología - Martes a Viernes
        timeSlots[1] = GenerateTimeSlots(specialistId: 1, startDate: today.AddDays(1), workingDays: new[] { 2, 3, 4, 5 }, startHour: 10, endHour: 17);

        // Especialista 2: Medicina Felina - Lunes a Jueves
        timeSlots[2] = GenerateTimeSlots(specialistId: 2, startDate: today, workingDays: new[] { 1, 2, 3, 4 }, startHour: 9, endHour: 16);

        // Especialista 3: Endocrinología - Miércoles a Viernes
        timeSlots[3] = GenerateTimeSlots(specialistId: 3, startDate: today.AddDays(2), workingDays: new[] { 3, 4, 5 }, startHour: 11, endHour: 18);

        // Especialista 4: Cardiología - Lunes a Miércoles
        timeSlots[4] = GenerateTimeSlots(specialistId: 4, startDate: today, workingDays: new[] { 1, 2, 3 }, startHour: 14, endHour: 19);

        return timeSlots;
    }

    /// <summary>
    /// Método auxiliar para generar franjas horarias para un especialista
    /// </summary>
    private List<TimeSlot> GenerateTimeSlots(int specialistId, DateTime startDate, int[] workingDays, int startHour, int endHour)
    {
        var slots = new List<TimeSlot>();
        var slotId = (specialistId * 1000); // ID único basado en especialista

        // Generar 14 días de horarios
        for (int dayOffset = 0; dayOffset < 14; dayOffset++)
        {
            var date = startDate.AddDays(dayOffset);

            // Verificar que sea un día de trabajo (0=Domingo, 1=Lunes, ..., 6=Sábado)
            if (!workingDays.Contains((int)date.DayOfWeek))
                continue;

            // Generar franjas de 30 minutos
            for (int hour = startHour; hour < endHour; hour++)
            {
                slots.Add(new TimeSlot
                {
                    Id = slotId++,
                    SpecialistId = specialistId,
                    Date = date,
                    StartTime = new TimeSpan(hour, 0, 0),
                    EndTime = new TimeSpan(hour, 30, 0),
                    IsAvailable = true,
                    ReservationId = null
                });

                slots.Add(new TimeSlot
                {
                    Id = slotId++,
                    SpecialistId = specialistId,
                    Date = date,
                    StartTime = new TimeSpan(hour, 30, 0),
                    EndTime = new TimeSpan(hour + 1, 0, 0),
                    IsAvailable = true,
                    ReservationId = null
                });
            }

            // Simular que algunas horas están ocupadas (aleatoriamente)
            if (slots.Count > 5)
            {
                var random = new Random(specialistId + dayOffset); // Seed para reproducibilidad
                for (int i = 0; i < slots.Count; i++)
                {
                    if (random.Next(0, 100) > 70) // 30% ocupadas
                    {
                        slots[slots.Count - i - 1].IsAvailable = false;
                    }
                }
            }
        }

        return slots;
    }
}
