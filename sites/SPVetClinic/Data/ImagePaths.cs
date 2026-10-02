namespace SPVetClinic.Data;

/// <summary>
/// Rutas centralizadas de todas las imágenes del sitio.
///
/// Dos capas:
///   1. <see cref="Library"/> — inventario: una clase por carpeta de wwwroot/images, con TODAS las fotos.
///      Se actualiza al agregar una foto (después de correr scripts/optimization/optimize-images.py).
///   2. Roles (<see cref="Hero"/>, <see cref="Services"/>, <see cref="Clinic"/>, <see cref="PageHeroes"/>) —
///      dónde se usa cada foto. Las páginas usan SOLO los roles; cambiar la foto de un hero es cambiar
///      una línea aquí, sin tocar ningún .razor.
///
/// Los originales (varios MB) viven en assets-source/ y NO se publican; aquí solo hay AVIF livianos.
/// </summary>
public static class ImagePaths
{
    private const string Root = "images/";

    // ── Marca ─────────────────────────────────────────────────────────────
    public const string Logo = Root + "brand/logo.png";

    // ══════════════════════════════════════════════════════════════════════
    // 1. INVENTARIO — una clase por carpeta
    // ══════════════════════════════════════════════════════════════════════
    public static class Library
    {
        /// <summary>images/clinic/exterior — fachada y entorno</summary>
        public static class Exterior
        {
            public const string Frontage = Root + "clinic/exterior/frontage.avif";
            public const string Frontage2 = Root + "clinic/exterior/frontage-2.avif";
            public const string Frontage3 = Root + "clinic/exterior/frontage-3.avif";
            public const string Frontage4 = Root + "clinic/exterior/frontage-4.avif";
            public const string FrontageLogoCloseUp = Root + "clinic/exterior/frontage-logo-close-up.avif";
            public const string SaintPabloSculpture = Root + "clinic/exterior/saint-pablo-sculpture.avif";
        }

        /// <summary>images/clinic/interior — recepción y salas de espera</summary>
        public static class Interior
        {
            public const string ReceptionArea = Root + "clinic/interior/reception-area.avif";
            public const string ReceptionDesk = Root + "clinic/interior/reception-desk.avif";
            public const string ReceptionDeskSucursal = Root + "clinic/interior/reception-desk-sucursal.avif";
            public const string WaitingRoomCat = Root + "clinic/interior/waiting-room-cat.avif";
            public const string WaitingRoomSucursal = Root + "clinic/interior/waiting-room-sucursal.avif";
            public const string WaitingRoomSucursal2 = Root + "clinic/interior/waiting-room-sucursal-2.avif";
        }

        /// <summary>images/consult — consultas y boxes de atención</summary>
        public static class Consult
        {
            public const string ConsultDog = Root + "consult/consult-dog.avif";
            public const string ConsultGeneral = Root + "consult/consult-general.avif";
            public const string DogInConsult = Root + "consult/dog-in-consult.avif";
            public const string CatInConsult = Root + "consult/cat-in-consult.avif";
        }

        /// <summary>images/diagnostics — imagenología</summary>
        public static class Diagnostics
        {
            public const string Xray = Root + "diagnostics/xray.avif";
            public const string UltrasoundLeft = Root + "diagnostics/ultrasound-left.avif";
            public const string UltrasoundRight = Root + "diagnostics/ultrasound-right.avif";
            public const string UltrasoundProcedure = Root + "diagnostics/ultrasound-procedure.avif";
        }

        /// <summary>images/lab — laboratorio</summary>
        public static class Lab
        {
            public const string WideShot = Root + "lab/wide-shot.avif";
            public const string IdexxMonitorCloseUp = Root + "lab/idexx-monitor-close-up.avif";
            public const string IdexxSnapCloseUp = Root + "lab/idexx-snap-close-up.avif";
        }

        /// <summary>images/surgery — pabellón</summary>
        public static class Surgery
        {
            public const string Procedure = Root + "surgery/procedure.avif";
            public const string Procedure2 = Root + "surgery/procedure-2.avif";
            public const string ProcedureCloseUp = Root + "surgery/procedure-close-up.avif";
            public const string Monitor = Root + "surgery/monitor.avif";
            public const string SurgicalLight = Root + "surgery/surgical-light.avif";
            public const string EndotrachealTube = Root + "surgery/endotracheal-tube.avif";
        }

        /// <summary>images/hospital</summary>
        public static class Hospital
        {
            public const string DogKennelsCloseUp = Root + "hospital/dog-kennels-close-up.avif";
        }

        /// <summary>images/grooming</summary>
        public static class Grooming
        {
            public const string HairSalon = Root + "grooming/hair-salon.avif";
        }

        /// <summary>images/vetmovil</summary>
        public static class VetMovil
        {
            public const string LeftSide = Root + "vetmovil/left-side.avif";
            public const string RightBackSide = Root + "vetmovil/right-back-side.avif";
            public const string BackSide = Root + "vetmovil/back-side.avif";
        }

        /// <summary>images/patients — pacientes</summary>
        public static class Patients
        {
            public const string CatExtremeCloseUp = Root + "patients/cat-extreme-close-up.avif";
            public const string CatPlayRoom = Root + "patients/cat-play-room.avif";
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 2. ROLES — dónde se usa cada foto (lo que referencian las páginas)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Carrusel principal del Home</summary>
    public static class Hero
    {
        public const string Frontage = Library.Exterior.Frontage;

        /// <summary>Slide "Especialistas certificados" del carrusel del Home (equipo operando)</summary>
        public const string HomeEspecialistas = Library.Surgery.ProcedureCloseUp;
    }

    /// <summary>Tarjetas de servicios (Home / Servicios)</summary>
    public static class Services
    {
        public const string Consulta = Library.Consult.DogInConsult;
        public const string Cirugia = Library.Surgery.SurgicalLight;
        public const string Examenes = Library.Lab.IdexxMonitorCloseUp;
        public const string Laboratory = Library.Lab.IdexxSnapCloseUp;
        public const string VetMovil = Library.VetMovil.LeftSide;
        public const string Grooming = Library.Grooming.HairSalon;
        public const string Vacunacion = Library.Consult.CatInConsult;
    }

    /// <summary>Instalaciones (Conócenos y secciones de la clínica)</summary>
    public static class Clinic
    {
        public const string Reception = Library.Interior.ReceptionArea;
        public const string ReceptionDesk = Library.Interior.ReceptionDesk;
        public const string SpecialistReception = Library.Interior.WaitingRoomSucursal;
        public const string WaitingRoom = Library.Interior.WaitingRoomCat;
        public const string Hospitalizacion = Library.Hospital.DogKennelsCloseUp;
        public const string Laboratorio = Library.Lab.WideShot;
        public const string Pabellon = Library.Surgery.ProcedureCloseUp;

        /// <summary>Sala de juegos para gatos (galería de instalaciones de Conócenos)</summary>
        public const string CatRoom = Library.Patients.CatPlayRoom;

        /// <summary>Foto de la clínica para la sección "Nuestra Historia" de Conócenos</summary>
        public const string Historia = Library.Exterior.Frontage2;

        /// <summary>Fachada de la clínica (usa la foto del hero)</summary>
        public const string Fachada = Hero.Frontage;

        // Pendiente — asignar la ruta cuando se tenga la foto
        public const string Mapa = "";
    }

    /// <summary>Foto de fondo del hero de cada página interna</summary>
    public static class PageHeroes
    {
        public const string Servicios = Library.Surgery.Procedure2;
        public const string Especialistas = Library.Consult.CatInConsult;
        public const string Examenes = Library.Diagnostics.UltrasoundLeft;
        public const string VetMovil = Services.VetMovil;
        public const string Hospitalizacion = Clinic.Hospitalizacion;

        /// <summary>Mesón de recepción (sucursal). No repetir en la galería de Conócenos: ahí va Clinic.ReceptionDesk.</summary>
        public const string Conocenos = Library.Interior.ReceptionDeskSucursal;
    }
}
