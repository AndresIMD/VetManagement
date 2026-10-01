namespace SPVetClinic.Data.Models.Booking;

/// <summary>
/// Tipo de transacción en WebPay
/// </summary>
public enum WebPayTransactionType
{
    /// <summary>Transacción de compra</summary>
    Purchase,

    /// <summary>Devolución o reembolso</summary>
    Refund
}

/// <summary>
/// Modelo que representa un pago mediante WebPay Plus
/// </summary>
public class WebPayTransaction
{
    /// <summary>Identificador único de la transacción</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>ID de la reserva asociada</summary>
    public string ReservationId { get; set; } = "";

    /// <summary>Tipo de transacción</summary>
    public WebPayTransactionType TransactionType { get; set; } = WebPayTransactionType.Purchase;

    /// <summary>Orden de compra generada por WebPay</summary>
    public string OrderId { get; set; } = "";

    /// <summary>Monto en pesos chilenos (CLP)</summary>
    public decimal Amount { get; set; }

    /// <summary>Moneda (siempre CLP para Chile)</summary>
    public string Currency { get; set; } = "CLP";

    /// <summary>Código de respuesta de WebPay</summary>
    public string? ResponseCode { get; set; }

    /// <summary>Mensaje de respuesta de WebPay</summary>
    public string? ResponseMessage { get; set; }

    /// <summary>Token de sesión de WebPay</summary>
    public string? SessionToken { get; set; }

    /// <summary>URL de redirección a WebPay</summary>
    public string? RedirectUrl { get; set; }

    /// <summary>Indica si la transacción fue exitosa</summary>
    public bool IsSuccessful { get; set; } = false;

    /// <summary>Token de la tarjeta (NO almacenar datos sensibles reales)</summary>
    public string? CardToken { get; set; }

    /// <summary>Últimos 4 dígitos de la tarjeta</summary>
    public string? CardLast4Digits { get; set; }

    /// <summary>Tipo de tarjeta (Visa, Mastercard, etc.)</summary>
    public string? CardType { get; set; }

    /// <summary>Fecha y hora de creación de la transacción</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora de procesamiento</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>IP del cliente que realizó la transacción</summary>
    public string? ClientIpAddress { get; set; }

    /// <summary>User Agent del navegador</summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Modelo para respuesta de WebPay durante el proceso de pago
/// </summary>
public class WebPayResponse
{
    /// <summary>Código de respuesta</summary>
    public string? Code { get; set; }

    /// <summary>Mensaje descriptivo</summary>
    public string? Message { get; set; }

    /// <summary>URL de redirección después del pago</summary>
    public string? Url { get; set; }

    /// <summary>Token de la transacción</summary>
    public string? Token { get; set; }

    /// <summary>Datos adicionales de respuesta</summary>
    public Dictionary<string, object>? AdditionalData { get; set; }
}
