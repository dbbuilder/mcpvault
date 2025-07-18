// MCPVault Landing Page JavaScript

// Smooth scroll for navigation links
document.addEventListener('DOMContentLoaded', function() {
    // Mobile menu toggle
    const mobileMenuButton = document.getElementById('mobile-menu-button');
    const mobileMenu = document.getElementById('mobile-menu');
    
    if (mobileMenuButton && mobileMenu) {
        mobileMenuButton.addEventListener('click', () => {
            mobileMenu.classList.toggle('hidden');
        });
    }

    // Smooth scrolling for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
                // Close mobile menu if open
                if (mobileMenu && !mobileMenu.classList.contains('hidden')) {
                    mobileMenu.classList.add('hidden');
                }
            }
        });
    });

    // Navbar background on scroll
    const navbar = document.getElementById('navbar');
    if (navbar) {
        window.addEventListener('scroll', () => {
            if (window.scrollY > 50) {
                navbar.classList.add('bg-gray-900/95', 'backdrop-blur-md', 'shadow-lg');
                navbar.classList.remove('bg-transparent');
            } else {
                navbar.classList.add('bg-transparent');
                navbar.classList.remove('bg-gray-900/95', 'backdrop-blur-md', 'shadow-lg');
            }
        });
    }

    // Intersection Observer for fade-in animations
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in-up');
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);

    // Observe all elements with animate-on-scroll class
    document.querySelectorAll('.animate-on-scroll').forEach(el => {
        observer.observe(el);
    });

    // Terminal typing effect
    const terminalElement = document.querySelector('.terminal-typing');
    if (terminalElement) {
        const commands = [
            { text: '$ mcpvault init', delay: 100 },
            { text: '\nInitializing MCPVault...', delay: 500 },
            { text: '\n✓ Scanning MCP servers', delay: 300 },
            { text: '\n✓ Validating compliance rules', delay: 300 },
            { text: '\n✓ Setting up audit trails', delay: 300 },
            { text: '\n✓ Configuring access controls', delay: 300 },
            { text: '\n\nMCPVault initialized successfully!', delay: 500 }
        ];

        let currentIndex = 0;
        let currentChar = 0;
        let currentText = '';

        function typeCommand() {
            if (currentIndex < commands.length) {
                const command = commands[currentIndex];
                if (currentChar < command.text.length) {
                    currentText += command.text[currentChar];
                    terminalElement.innerHTML = currentText + '<span class="terminal-cursor">_</span>';
                    currentChar++;
                    setTimeout(typeCommand, 50);
                } else {
                    currentChar = 0;
                    currentIndex++;
                    setTimeout(typeCommand, commands[currentIndex - 1].delay);
                }
            } else {
                // Remove cursor after typing is complete
                terminalElement.innerHTML = currentText;
            }
        }

        // Start typing when terminal is in view
        const terminalObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    typeCommand();
                    terminalObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.5 });

        terminalObserver.observe(terminalElement);
    }

    // Pricing toggle
    const pricingToggle = document.getElementById('pricing-toggle');
    const monthlyPrices = document.querySelectorAll('.monthly-price');
    const annualPrices = document.querySelectorAll('.annual-price');

    if (pricingToggle) {
        pricingToggle.addEventListener('change', () => {
            if (pricingToggle.checked) {
                // Show annual prices
                monthlyPrices.forEach(el => el.classList.add('hidden'));
                annualPrices.forEach(el => el.classList.remove('hidden'));
            } else {
                // Show monthly prices
                monthlyPrices.forEach(el => el.classList.remove('hidden'));
                annualPrices.forEach(el => el.classList.add('hidden'));
            }
        });
    }

    // Form submission handling
    const ctaForm = document.getElementById('cta-form');
    if (ctaForm) {
        ctaForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const email = e.target.email.value;
            const submitButton = e.target.querySelector('button[type="submit"]');
            const originalText = submitButton.textContent;

            // Show loading state
            submitButton.disabled = true;
            submitButton.innerHTML = '<span class="loading-pulse"></span><span class="loading-pulse"></span><span class="loading-pulse"></span>';

            // Simulate API call
            setTimeout(() => {
                submitButton.disabled = false;
                submitButton.textContent = 'Thank you!';
                ctaForm.reset();
                
                // Show success message
                const successMessage = document.createElement('div');
                successMessage.className = 'mt-4 p-4 bg-green-500/10 border border-green-500 rounded-lg text-green-400 animate-fade-in';
                successMessage.textContent = 'Thanks for your interest! We\'ll be in touch soon.';
                ctaForm.appendChild(successMessage);

                // Reset after 5 seconds
                setTimeout(() => {
                    submitButton.textContent = originalText;
                    successMessage.remove();
                }, 5000);
            }, 2000);
        });
    }

    // Copy code snippets
    document.querySelectorAll('.copy-code').forEach(button => {
        button.addEventListener('click', () => {
            const code = button.previousElementSibling.textContent;
            navigator.clipboard.writeText(code).then(() => {
                const originalText = button.textContent;
                button.textContent = 'Copied!';
                button.classList.add('bg-green-600');
                setTimeout(() => {
                    button.textContent = originalText;
                    button.classList.remove('bg-green-600');
                }, 2000);
            });
        });
    });

    // Parallax effect for hero section
    const heroSection = document.querySelector('.hero-section');
    if (heroSection) {
        window.addEventListener('scroll', () => {
            const scrolled = window.pageYOffset;
            const parallaxSpeed = 0.5;
            heroSection.style.transform = `translateY(${scrolled * parallaxSpeed}px)`;
        });
    }
});

// Terminal cursor blink
const style = document.createElement('style');
style.textContent = `
    .terminal-cursor {
        animation: blink 1s infinite;
    }
    @keyframes blink {
        0%, 50% { opacity: 1; }
        51%, 100% { opacity: 0; }
    }
`;
document.head.appendChild(style);