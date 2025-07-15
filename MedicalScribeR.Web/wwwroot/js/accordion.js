document.addEventListener('DOMContentLoaded', () => {
    const accordionItems = document.querySelectorAll('.accordion-item');

    accordionItems.forEach(item => {
        const header = item.querySelector('.accordion-header');
        const content = item.querySelector('.accordion-content');

        // Set initial state for GSAP
        gsap.set(content, { maxHeight: 0, overflow: 'hidden' });

        header.addEventListener('click', () => {
            const isOpen = item.classList.contains('active');

            // Close all other items
            accordionItems.forEach(otherItem => {
                if (otherItem !== item) {
                    otherItem.classList.remove('active');
                    gsap.to(otherItem.querySelector('.accordion-content'), { maxHeight: 0, duration: 0.3, ease: 'power2.inOut' });
                }
            });

            // Toggle the clicked item
            if (isOpen) {
                item.classList.remove('active');
                gsap.to(content, { maxHeight: 0, duration: 0.3, ease: 'power2.inOut' });
            } else {
                item.classList.add('active');
                gsap.to(content, { maxHeight: content.scrollHeight, duration: 0.3, ease: 'power2.inOut' });
            }
        });
    });
});
