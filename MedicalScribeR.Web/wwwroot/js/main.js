
document.addEventListener('DOMContentLoaded', () => {
    gsap.registerPlugin(ScrollTrigger);

    // 1. Animação de Entrada da Página
    const timeline = gsap.timeline({ defaults: { ease: "power3.out" } });

    timeline.from(".logo", { duration: 1, opacity: 0, scale: 0.5, delay: 0.2 })
            .from(".nav-links a", { duration: 0.8, opacity: 0, y: -30, stagger: 0.2 }, "-=0.5")
            .from(".hero-content h1", { duration: 1, opacity: 0, y: 50 }, "-=0.5")
            .from(".hero-content p", { duration: 1, opacity: 0, y: 30 }, "-=0.7")
            .from(".hero-content .cta-button", { duration: 1, opacity: 0, y: 20 }, "-=0.7")
            .from(".feature-card", { duration: 0.8, opacity: 0, y: 50, stagger: 0.2 }, "-=0.5");

    // 2. Animar o Logo (assumindo que o logo é um SVG com a estrutura correta)
    const logo = document.querySelector('.logo');
    if (logo && logo.tagName.toLowerCase() === 'svg') {
        const logoParts = logo.querySelectorAll('.logo-part'); // Seletor para as partes do ícone
        const logoText = logo.querySelector('.logo-text'); // Seletor para o texto

        logo.addEventListener('mouseenter', () => {
            gsap.to(logoParts, { 
                duration: 0.4, 
                x: (i) => (i % 2 === 0 ? -5 : 5), // Afasta as partes do centro
                y: (i) => (i < 2 ? -5 : 5),
                rotation: 45, 
                transformOrigin: "center center",
                ease: "power2.out" 
            });
            gsap.to(logoText, { duration: 0.4, fill: "#42a5f5" }); // Muda a cor do texto
        });

        logo.addEventListener('mouseleave', () => {
            gsap.to(logoParts, { 
                duration: 0.4, 
                x: 0, 
                y: 0,
                rotation: 0, 
                ease: "power2.inOut" 
            });
            gsap.to(logoText, { duration: 0.4, fill: "#0d47a1" }); // Retorna à cor original
        });
    }

    // 3. Animações Controladas pelo Scroll (Scroll-Triggered)
    gsap.utils.toArray('section').forEach((section, i) => {
        gsap.from(section, {
            scrollTrigger: {
                trigger: section,
                start: "top 80%",
                end: "bottom 20%",
                toggleActions: "play none none none"
            },
            opacity: 0,
            y: 50,
            duration: 1,
            ease: "power3.out"
        });
    });

    // Barra de Progresso
    gsap.to("body", {
        scrollTrigger: {
            trigger: "body",
            start: "top top",
            end: "bottom bottom",
            scrub: 1,
            onUpdate: self => {
                const progress = self.progress.toFixed(2);
                gsap.to(".progress-bar", { width: `${progress * 100}%`, ease: "none" });
            }
        }
    });
});
