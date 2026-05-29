/**
 * Aura Cinema Toast Notification System
 */

const toastContainer = document.createElement('div');
toastContainer.id = 'toast-container';
document.body.appendChild(toastContainer);

function showToast(message, type = 'success') {
    const toast = document.createElement('div');
    toast.className = `ac-toast ac-toast-${type}`;
    
    let iconClass = 'bi-check-circle-fill';
    if (type === 'error') iconClass = 'bi-exclamation-circle-fill';
    if (type === 'info') iconClass = 'bi-info-circle-fill';
    if (type === 'warning') iconClass = 'bi-exclamation-triangle-fill';

    toast.innerHTML = `
        <div class="ac-toast-icon">
            <i class="bi ${iconClass}"></i>
        </div>
        <div class="ac-toast-message">${message}</div>
    `;

    toastContainer.appendChild(toast);

    // Trigger animation
    setTimeout(() => {
        toast.classList.add('show');
    }, 10);

    // Auto remove after 2 seconds (2000ms) + animation time
    setTimeout(() => {
        toast.classList.remove('show');
        toast.classList.add('hide');
        setTimeout(() => {
            toast.remove();
        }, 400); // Wait for transition to finish
    }, 2000);
}

// Global exposure
window.showToast = showToast;
