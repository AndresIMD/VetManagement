// ==========================================
// SCROLL ANIMATIONS
// ==========================================

// Persistent observer — created once, reused on every call
window._revealObserver = window._revealObserver || new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.classList.add('active');
        }
    });
}, {
    threshold: 0.1,
    rootMargin: '0px 0px -50px 0px'
});

window._scrollSetup = false;

window.initScrollAnimations = function () {
    // Observe any new .reveal elements (safe to call multiple times)
    document.querySelectorAll('.reveal:not(.active), .reveal-left:not(.active), .reveal-right:not(.active)')
        .forEach(el => window._revealObserver.observe(el));

    // One-time setup for scroll and anchor listeners
    if (!window._scrollSetup) {
        window._scrollSetup = true;

        // Smooth scroll for anchor links (delegated to avoid duplicates)
        document.addEventListener('click', function (e) {
            const anchor = e.target.closest('a[href^="#"]');
            if (!anchor) return;
            e.preventDefault();
            const target = document.querySelector(anchor.getAttribute('href'));
            if (target) {
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            const navLinks = document.getElementById('navLinks');
            if (navLinks) navLinks.classList.remove('active');
        });

        // Navbar scroll effect
        window.addEventListener('scroll', () => {
            const nav = document.querySelector('.topnav');
            if (nav) {
                if (window.scrollY > 50) {
                    nav.classList.add('scrolled');
                } else {
                    nav.classList.remove('scrolled');
                }
            }
        });
    }
};

// ==========================================
// CLEAR TEXT SELECTION
// ==========================================
window.clearTextSelection = function () {
    if (window.getSelection) {
        window.getSelection().removeAllRanges();
    }

    // En GitHub Pages, el script de redirección SPA puede causar
    // re-selección después de history.replaceState
    // Ejecutar nuevamente después de un frame para asegurar limpieza
    requestAnimationFrame(() => {
        if (window.getSelection) {
            window.getSelection().removeAllRanges();
        }
    });
};

// ==========================================
// FAQ TOGGLE
// ==========================================
window.toggleFaq = function (button) {
    const faqItem = button.closest('.faq-item');
    const answer = faqItem.querySelector('.faq-answer');
    const icon = button.querySelector('.faq-icon');

    faqItem.classList.toggle('active');

    if (faqItem.classList.contains('active')) {
        answer.style.maxHeight = answer.scrollHeight + 'px';
        icon.textContent = '−';
    } else {
        answer.style.maxHeight = '0';
        icon.textContent = '+';
    }
};

// ==========================================
// MOBILE MENU & DROPDOWN (TopNav)
// ==========================================
window.toggleMobileMenu = function () {
    document.getElementById('navLinks').classList.toggle('active');
    var btn = document.querySelector('.mobile-menu-btn');
    if (btn) {
        btn.classList.toggle('active');
        btn.setAttribute('aria-expanded', btn.classList.contains('active'));
    }
};

function setDropdownExpanded(dropdown) {
    var b = dropdown.querySelector('.nav-dropdown-btn');
    if (b) b.setAttribute('aria-expanded', dropdown.classList.contains('active'));
}

// Todos los desplegables (menú móvil, "Servicios" y botón flotante de emergencia) se cierran
// con un clic fuera de ellos. Un solo listener global para el navbar; el FAB tiene el suyo más
// abajo. Se usa composedPath() y no event.target: Blazor puede reemplazar el nodo pulsado y el
// target quedaría fuera del DOM. Por eso tampoco hace falta stopPropagation: los demás
// desplegables (p. ej. el FAB) tienen que ver el clic para cerrarse.
document.addEventListener('click', function (event) {
    // El FAB se cierra con un click() sintético (ver abajo): no es un clic del usuario fuera del menú
    if (window.__fabClosing) return;
    var path = event.composedPath();
    var navLinks = document.getElementById('navLinks');
    var btn = document.querySelector('.mobile-menu-btn');

    // Un clic en un enlace del navbar (Inicio, Contacto, ítems de Servicios...) navega: cierra todo
    var followedLink = navLinks && path.some(function (el) { return el.tagName === 'A' && navLinks.contains(el); });

    document.querySelectorAll('.nav-dropdown.active').forEach(function (d) {
        if (followedLink || !path.includes(d)) {
            d.classList.remove('active');
            setDropdownExpanded(d);
        }
    });

    if (navLinks && navLinks.classList.contains('active') &&
        (followedLink || (!path.includes(navLinks) && !(btn && path.includes(btn))))) {
        navLinks.classList.remove('active');
        if (btn) { btn.classList.remove('active'); btn.setAttribute('aria-expanded', 'false'); }
    }
});

window.toggleDropdown = function (event) {
    var dropdown = event.target.closest('.nav-dropdown');
    dropdown.classList.toggle('active');
    setDropdownExpanded(dropdown);
};

// Close dropdown on mobile when clicking a link
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.dropdown-item').forEach(function (item) {
        item.addEventListener('click', function () {
            document.querySelectorAll('.nav-dropdown').forEach(function (d) { d.classList.remove('active'); setDropdownExpanded(d); });
            var navLinks = document.getElementById('navLinks');
            if (navLinks) navLinks.classList.remove('active');
            var btn = document.querySelector('.mobile-menu-btn');
            if (btn) { btn.classList.remove('active'); btn.setAttribute('aria-expanded', 'false'); }
        });
    });

});

// ==========================================
// EMERGENCY FAB — CLOSE ON CLICK OUTSIDE
// ==========================================
let fabOutsideHandler = null;
window.attachClickOutsideHandler = function (fabElement) {
    // Un solo listener aunque el componente se vuelva a montar
    if (fabOutsideHandler) document.removeEventListener('click', fabOutsideHandler);
    fabOutsideHandler = function (event) {
        // composedPath() se calcula al despachar el evento. event.target no sirve: al abrir,
        // Blazor reemplaza el ícono del botón y el target queda fuera del DOM, con lo que
        // el clic sobre el ícono parecía "fuera" y cerraba el panel al instante.
        if (fabElement && !event.composedPath().includes(fabElement)) {
            // Find the Blazor component button to trigger close
            const fabBtn = fabElement.querySelector('.efab__btn');
            if (fabBtn) {
                // Check if FAB is open (has efab--open class on parent)
                if (fabElement.classList.contains('efab--open')) {
                    // click() es síncrono: la bandera evita que el listener del navbar lo tome
                    // como un clic fuera y cierre el desplegable que el usuario acaba de abrir
                    window.__fabClosing = true;
                    try { fabBtn.click(); } finally { window.__fabClosing = false; }
                }
            }
        }
    };
    document.addEventListener('click', fabOutsideHandler);
};
