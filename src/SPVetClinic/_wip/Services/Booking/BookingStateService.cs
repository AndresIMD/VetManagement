namespace SPVetClinic.Services.Booking;

using SPVetClinic.Data.Models.Booking;

/// <summary>
/// Pasos del flujo de reserva
/// </summary>
public enum BookingStep
{
    /// <summary>Seleccionar especialista</summary>
    SpecialistSelection,

    /// <summary>Seleccionar franja horaria</summary>
    TimeSlotSelection,

    /// <summary>Completar formulario de datos</summary>
    FormData,

    /// <summary>Revisión y confirmación</summary>
    Review,

    /// <summary>Procesamiento de pago</summary>
    Payment,

    /// <summary>Reserva completada</summary>
    Completed
}

/// <summary>
/// Servicio que gestiona el estado de la reserva a través de múltiples componentes
/// Implementa patrón Scoped para compartir estado durante la sesión del usuario
/// </summary>
public class BookingStateService
{
    private BookingStep _currentStep = BookingStep.SpecialistSelection;
    private BookingFormData _formData = new();
    private Reservation? _currentReservation;
    private WebPayTransaction? _currentTransaction;
    private List<Specialist> _specialists = new();
    private Dictionary<int, List<TimeSlot>> _timeSlotsBySpecialist = new();
    private string? _errorMessage;
    private bool _isLoading = false;

    /// <summary>Se dispara cuando cambia el paso actual</summary>
    public event Action? OnStepChanged;

    /// <summary>Se dispara cuando cambian los datos del formulario</summary>
    public event Action? OnFormDataChanged;

    /// <summary>Se dispara cuando hay un error</summary>
    public event Action? OnError;

    /// <summary>Se dispara cuando se completa una reserva</summary>
    public event Action? OnReservationCompleted;

    /// <summary>Obtiene el paso actual del flujo</summary>
    public BookingStep CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep != value)
            {
                _currentStep = value;
                OnStepChanged?.Invoke();
            }
        }
    }

    /// <summary>Obtiene los datos actuales del formulario</summary>
    public BookingFormData FormData => _formData;

    /// <summary>Obtiene la reserva actual (después de completar el formulario)</summary>
    public Reservation? CurrentReservation => _currentReservation;

    /// <summary>Obtiene la transacción de pago actual</summary>
    public WebPayTransaction? CurrentTransaction => _currentTransaction;

    /// <summary>Obtiene el mensaje de error actual</summary>
    public string? ErrorMessage => _errorMessage;

    /// <summary>Indica si se está cargando información</summary>
    public bool IsLoading => _isLoading;

    /// <summary>Obtiene la lista de especialistas disponibles</summary>
    public IReadOnlyList<Specialist> Specialists => _specialists.AsReadOnly();

    /// <summary>
    /// Obtiene las franjas horarias para un especialista específico
    /// </summary>
    public IReadOnlyList<TimeSlot> GetTimeSlots(int specialistId)
    {
        return _timeSlotsBySpecialist.ContainsKey(specialistId)
            ? _timeSlotsBySpecialist[specialistId].AsReadOnly()
            : new List<TimeSlot>().AsReadOnly();
    }

    /// <summary>
    /// Obtiene las fechas únicas disponibles para un especialista
    /// </summary>
    public IReadOnlyList<DateTime> GetAvailableDates(int specialistId)
    {
        var timeSlots = GetTimeSlots(specialistId);
        return timeSlots
            .Where(ts => ts.IsAvailable)
            .Select(ts => ts.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Obtiene las franjas horarias para una fecha específica y especialista
    /// </summary>
    public IReadOnlyList<TimeSlot> GetTimeSlotsForDate(int specialistId, DateTime date)
    {
        var timeSlots = GetTimeSlots(specialistId);
        return timeSlots
            .Where(ts => ts.Date.Date == date.Date && ts.IsAvailable)
            .OrderBy(ts => ts.StartTime)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Inicializa el servicio con datos de especialistas y franjas horarias
    /// </summary>
    public async Task InitializeAsync(List<Specialist> specialists, Dictionary<int, List<TimeSlot>> timeSlots)
    {
        try
        {
            SetLoading(true);
            _specialists = specialists;
            _timeSlotsBySpecialist = timeSlots;
            ClearError();
        }
        catch (Exception ex)
        {
            SetError($"Error al inicializar: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Selecciona un especialista y avanza al paso de selección de franja horaria
    /// </summary>
    public void SelectSpecialist(Specialist specialist)
    {
        _formData.SelectedSpecialist = specialist;
        OnFormDataChanged?.Invoke();
        ClearError();
        NextStep();
    }

    /// <summary>
    /// Selecciona una franja horaria y avanza al paso de formulario
    /// </summary>
    public void SelectTimeSlot(TimeSlot timeSlot)
    {
        _formData.SelectedTimeSlot = timeSlot;
        OnFormDataChanged?.Invoke();
        ClearError();
        NextStep();
    }

    /// <summary>
    /// Actualiza los datos del propietario
    /// </summary>
    public void UpdateOwnerData(string name, string email, string phone)
    {
        _formData.OwnerName = name;
        _formData.OwnerEmail = email;
        _formData.OwnerPhone = phone;
        OnFormDataChanged?.Invoke();
    }

    /// <summary>
    /// Actualiza los datos de la mascota
    /// </summary>
    public void UpdatePetData(string name, string species, string? breed = null, string? age = null)
    {
        _formData.PetName = name;
        _formData.PetSpecies = species;
        _formData.PetBreed = breed;
        _formData.PetAge = age;
        OnFormDataChanged?.Invoke();
    }

    /// <summary>
    /// Actualiza síntomas y notas
    /// </summary>
    public void UpdateSymptoms(string? symptoms, string? additionalNotes = null)
    {
        _formData.Symptoms = symptoms;
        _formData.AdditionalNotes = additionalNotes;
        OnFormDataChanged?.Invoke();
    }

    /// <summary>
    /// Valida y crea la reserva a partir de los datos del formulario
    /// </summary>
    public bool CreateReservation()
    {
        if (!_formData.IsValid())
        {
            SetError("Por favor completa todos los campos requeridos");
            return false;
        }

        try
        {
            _currentReservation = _formData.ToReservation();
            ClearError();
            return true;
        }
        catch (Exception ex)
        {
            SetError($"Error al crear reserva: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Inicia una transacción de pago
    /// </summary>
    public void InitializePayment(decimal amount)
    {
        _currentTransaction = new WebPayTransaction
        {
            ReservationId = _currentReservation?.Id ?? "",
            Amount = amount,
            Currency = "CLP"
        };
    }

    /// <summary>
    /// Actualiza el estado de la transacción de pago
    /// </summary>
    public void UpdatePaymentStatus(string orderId, bool isSuccessful, string? responseCode = null, string? message = null)
    {
        if (_currentTransaction != null)
        {
            _currentTransaction.OrderId = orderId;
            _currentTransaction.IsSuccessful = isSuccessful;
            _currentTransaction.ResponseCode = responseCode;
            _currentTransaction.ResponseMessage = message;
            _currentTransaction.ProcessedAt = DateTime.UtcNow;

            if (isSuccessful && _currentReservation != null)
            {
                _currentReservation.Status = ReservationStatus.Confirmed;
                _currentReservation.ConfirmedAt = DateTime.UtcNow;
                _currentReservation.WebPayOrderId = orderId;
            }
        }
    }

    /// <summary>
    /// Avanza al siguiente paso del flujo
    /// </summary>
    public void NextStep()
    {
        if (CurrentStep < BookingStep.Completed)
        {
            CurrentStep = CurrentStep + 1;
        }
    }

    /// <summary>
    /// Retrocede al paso anterior
    /// </summary>
    public void PreviousStep()
    {
        if (CurrentStep > BookingStep.SpecialistSelection)
        {
            CurrentStep = CurrentStep - 1;
        }
    }

    /// <summary>
    /// Va a un paso específico
    /// </summary>
    public void GoToStep(BookingStep step)
    {
        CurrentStep = step;
    }

    /// <summary>
    /// Completa la reserva y notifica a los suscriptores
    /// </summary>
    public void CompleteReservation()
    {
        CurrentStep = BookingStep.Completed;
        OnReservationCompleted?.Invoke();
    }

    /// <summary>
    /// Reinicia el flujo completo de reserva
    /// </summary>
    public void Reset()
    {
        CurrentStep = BookingStep.SpecialistSelection;
        _formData.Reset();
        _currentReservation = null;
        _currentTransaction = null;
        ClearError();
    }

    /// <summary>
    /// Establece un mensaje de error y notifica
    /// </summary>
    private void SetError(string message)
    {
        _errorMessage = message;
        OnError?.Invoke();
    }

    /// <summary>
    /// Limpia el mensaje de error
    /// </summary>
    private void ClearError()
    {
        _errorMessage = null;
    }

    /// <summary>
    /// Establece el estado de carga
    /// </summary>
    private void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
    }
}
