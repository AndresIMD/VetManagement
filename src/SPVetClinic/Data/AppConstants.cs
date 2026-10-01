namespace SPVetClinic.Data;

/// <summary>
/// Constantes y datos de configuración de la clínica.
/// Centraliza toda la información de contacto y redes sociales
/// para facilitar actualizaciones futuras.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Información general de la clínica
    /// </summary>
    public static class Clinic
    {
        public const string Name = "San Pablo Vet clínic";
        public const string City = "Rancagua";
        public const string Country = "Chile";
        public const string Address = "Av. Central 265";
        public const string FullAddress = "Av. Central 265, Rancagua, Chile";
        public const string Schedule = "Urgencias 24 horas, todos los días";
        public const string YearsOfService = "más de 10 años";
        public const string YearsOfServiceStat = "10+";

        /// <summary>Para frases que empiezan con la cifra: "Más de 10 años…" (la constante base va en minúscula)</summary>
        public static string YearsOfServiceCapitalized => char.ToUpper(YearsOfService[0]) + YearsOfService[1..];
        public const string BookingUrl = "https://vetsanpablo.crmveterinario.com/reserva_online";
    }

    /// <summary>
    /// Números de teléfono
    /// </summary>
    public static class Phone
    {
        /// <summary>WhatsApp principal de la clínica</summary>
        public const string MainWhatsApp = "56961900401";
        public const string MainWhatsAppFormatted = "+56 9 6190 0401";

        /// <summary>WhatsApp del servicio veterinario móvil</summary>
        public const string MobileVetWhatsApp = "56983835867";
        public const string MobileVetWhatsAppFormatted = "+56 9 8383 5867";

        /// <summary>Teléfono fijo de la clínica (con código de país: TelLink antepone "+")</summary>
        public const string Landline = "56722904717";
        public const string LandlineFormatted = "72 290 4717";
    }

    /// <summary>
    /// Información de Hospitalización
    /// </summary>
    public static class Hospital
    {
        /// <summary>No hay horario de llamadas fijo: el contacto directo se entrega al tutor por WhatsApp.</summary>
        public const string ContactMethod = "Contacto directo por WhatsApp";

        /// <summary>No hay ventana horaria fija: se agenda en la clínica al finalizar cada ingreso o visita.</summary>
        public const string VisitsInfo = "Se coordina en la clínica";

        /// <summary>No hay ventana horaria fija: depende de la evolución del paciente.</summary>
        public const string DischargeInfo = "Según indicación médica";

        public const string InfoPolicy = "La información del paciente se entrega solo al tutor responsable registrado en la ficha clínica";
    }

    /// <summary>
    /// Redes sociales
    /// </summary>
    public static class Social
    {
        public const string InstagramUrl = "https://www.instagram.com/spvetclinic";
        public const string FacebookUrl = "https://www.facebook.com/sanpablovetclinic";
    }

    /// <summary>
    /// URLs de Google Maps
    /// </summary>
    public static class Maps
    {
        // Embed URL con ubicación correcta de San Pablo Vet clínic (empresa)
        public const string EmbedUrl = "https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3300.7510740611997!2d-70.71600672427813!3d-34.17828147310979!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x9663456594efe5a9%3A0xedb27494ccccd065!2sSan%20Pablo%20Vet%20Cl%C3%ADnic!5e0!3m2!1ses!2scl!4v1765084763953!5m2!1ses!2scl";

        // Link directo a la empresa en Google Maps (con búsqueda por nombre)
        public const string CompanyDirectLink = "https://www.google.com/maps/search/San+Pablo+Vet+Clínic+Rancagua";

        // URL heredada (dirección general)
        public const string DirectionsUrl = "https://maps.google.com/?q=Av.+Central+265,+Rancagua,+Chile";
    }

    /// <summary>
    /// Helpers para generar URLs
    /// </summary>
    public static class Urls
    {
        public static string WhatsAppLink(string phoneNumber) => $"https://wa.me/{phoneNumber}";
        public static string WhatsAppMainLink => WhatsAppLink(Phone.MainWhatsApp);
        public static string WhatsAppMobileVetLink => WhatsAppLink(Phone.MobileVetWhatsApp);
        public static string TelLink(string phoneNumber) => $"tel:+{phoneNumber}";
    }
}
