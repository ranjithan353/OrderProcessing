// Configuration
const API_BASE_URL = 'https://localhost:7000/api'; // Update this with your API URL or APIM endpoint

// Initialize app
document.addEventListener('DOMContentLoaded', function() {
    initializeEventListeners();
    calculateTotal();
    loadOrders();
});

// Event Listeners
function initializeEventListeners() {
    // Form submission
    document.getElementById('orderForm').addEventListener('submit', handleOrderSubmit);
    
    // Add item button
    document.getElementById('addItemBtn').addEventListener('click', addOrderItem);
    
    // Refresh orders button
    document.getElementById('refreshOrdersBtn').addEventListener('click', loadOrders);
    
    // Calculate total on input change
    document.addEventListener('input', function(e) {
        if (e.target.classList.contains('quantity') || e.target.classList.contains('unit-price')) {
            calculateTotal();
        }
    });
}

// Add order item
function addOrderItem() {
    const itemsContainer = document.getElementById('orderItems');
    const newItem = document.createElement('div');
    newItem.className = 'order-item mb-3 p-3 border rounded';
    newItem.innerHTML = `
        <div class="row">
            <div class="col-md-4">
                <label class="form-label">Product ID *</label>
                <input type="text" class="form-control product-id" required>
            </div>
            <div class="col-md-4">
                <label class="form-label">Product Name *</label>
                <input type="text" class="form-control product-name" required>
            </div>
            <div class="col-md-2">
                <label class="form-label">Quantity *</label>
                <input type="number" class="form-control quantity" min="1" value="1" required>
            </div>
            <div class="col-md-2">
                <label class="form-label">Unit Price *</label>
                <input type="number" class="form-control unit-price" step="0.01" min="0.01" required>
            </div>
        </div>
        <div class="mt-2">
            <button type="button" class="btn btn-sm btn-outline-danger btn-remove">
                <i class="bi bi-trash me-1"></i>Remove
            </button>
        </div>
    `;
    itemsContainer.appendChild(newItem);
    
    // Add remove button listener
    newItem.querySelector('.btn-remove').addEventListener('click', function() {
        newItem.remove();
        calculateTotal();
    });
}

// Calculate total amount
function calculateTotal() {
    const items = document.querySelectorAll('.order-item');
    let total = 0;
    
    items.forEach(item => {
        const quantity = parseFloat(item.querySelector('.quantity').value) || 0;
        const unitPrice = parseFloat(item.querySelector('.unit-price').value) || 0;
        total += quantity * unitPrice;
    });
    
    document.getElementById('totalAmount').textContent = `$${total.toFixed(2)}`;
}

// Handle order submission
async function handleOrderSubmit(e) {
    e.preventDefault();
    
    const form = e.target;
    const submitBtn = form.querySelector('button[type="submit"]');
    const originalText = submitBtn.innerHTML;
    
    // Disable submit button
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Submitting...';
    
    try {
        // Collect form data
        const orderData = {
            customerName: document.getElementById('customerName').value,
            customerEmail: document.getElementById('customerEmail').value,
            shippingAddress: document.getElementById('shippingAddress').value,
            items: []
        };
        
        // Collect items
        document.querySelectorAll('.order-item').forEach(item => {
            orderData.items.push({
                productId: item.querySelector('.product-id').value,
                productName: item.querySelector('.product-name').value,
                quantity: parseInt(item.querySelector('.quantity').value),
                unitPrice: parseFloat(item.querySelector('.unit-price').value)
            });
        });
        
        // Submit order
        const response = await fetch(`${API_BASE_URL}/orders`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Ocp-Apim-Subscription-Key': getApiKey() // Add if using APIM
            },
            body: JSON.stringify(orderData)
        });
        
        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to create order');
        }
        
        const order = await response.json();
        
        // Show success message
        showAlert('success', `Order created successfully! Order ID: ${order.id}`);
        
        // Reset form
        form.reset();
        document.getElementById('orderItems').innerHTML = `
            <div class="order-item mb-3 p-3 border rounded">
                <div class="row">
                    <div class="col-md-4">
                        <label class="form-label">Product ID *</label>
                        <input type="text" class="form-control product-id" required>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Product Name *</label>
                        <input type="text" class="form-control product-name" required>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label">Quantity *</label>
                        <input type="number" class="form-control quantity" min="1" value="1" required>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label">Unit Price *</label>
                        <input type="number" class="form-control unit-price" step="0.01" min="0.01" required>
                    </div>
                </div>
            </div>
        `;
        calculateTotal();
        
        // Reload orders
        setTimeout(() => loadOrders(), 1000);
        
    } catch (error) {
        showAlert('danger', `Error: ${error.message}`);
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalText;
    }
}

// Load orders
async function loadOrders() {
    const container = document.getElementById('ordersContainer');
    container.innerHTML = '<div class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></div>';
    
    try {
        const response = await fetch(`${API_BASE_URL}/orders`, {
            headers: {
                'Ocp-Apim-Subscription-Key': getApiKey() // Add if using APIM
            }
        });
        
        if (!response.ok) {
            throw new Error('Failed to load orders');
        }
        
        const orders = await response.json();
        
        if (orders.length === 0) {
            container.innerHTML = '<div class="alert alert-info text-center">No orders found. Create your first order!</div>';
            return;
        }
        
        // Render orders
        container.innerHTML = orders.map(order => createOrderCard(order)).join('');
        
        // Add event listeners to view buttons
        container.querySelectorAll('.view-order-btn').forEach(btn => {
            btn.addEventListener('click', function() {
                const orderId = this.dataset.orderId;
                showOrderDetails(orders.find(o => o.id === orderId));
            });
        });
        
    } catch (error) {
        container.innerHTML = `<div class="alert alert-danger">Error loading orders: ${error.message}</div>`;
    }
}

// Create order card
function createOrderCard(order) {
    const statusClass = order.status.toLowerCase() === 'processed' ? 'status-processed' : 'status-created';
    const statusBadge = order.status.toLowerCase() === 'processed' 
        ? '<span class="badge bg-success">Processed</span>' 
        : '<span class="badge bg-info">Created</span>';
    
    return `
        <div class="card order-card ${statusClass} shadow-sm">
            <div class="card-body">
                <div class="row align-items-center">
                    <div class="col-md-8">
                        <h5 class="card-title">
                            <i class="bi bi-receipt me-2"></i>Order #${order.id.substring(0, 8)}
                        </h5>
                        <p class="card-text mb-1">
                            <strong>Customer:</strong> ${order.customerName}
                        </p>
                        <p class="card-text mb-1">
                            <strong>Email:</strong> ${order.customerEmail}
                        </p>
                        <p class="card-text mb-1">
                            <strong>Total:</strong> $${order.totalAmount.toFixed(2)}
                        </p>
                        <p class="card-text mb-0">
                            <strong>Created:</strong> ${new Date(order.createdAt).toLocaleString()}
                        </p>
                    </div>
                    <div class="col-md-4 text-md-end">
                        ${statusBadge}
                        <br>
                        <button class="btn btn-primary mt-2 view-order-btn" data-order-id="${order.id}">
                            <i class="bi bi-eye me-1"></i>View Details
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;
}

// Show order details
function showOrderDetails(order) {
    const modal = new bootstrap.Modal(document.getElementById('orderDetailsModal'));
    const content = document.getElementById('orderDetailsContent');
    
    const itemsHtml = order.items.map(item => `
        <tr>
            <td>${item.productId}</td>
            <td>${item.productName}</td>
            <td>${item.quantity}</td>
            <td>$${item.unitPrice.toFixed(2)}</td>
            <td>$${item.subTotal.toFixed(2)}</td>
        </tr>
    `).join('');
    
    content.innerHTML = `
        <div class="mb-3">
            <h6>Order Information</h6>
            <table class="table table-bordered">
                <tr>
                    <th>Order ID</th>
                    <td>${order.id}</td>
                </tr>
                <tr>
                    <th>Status</th>
                    <td><span class="badge ${order.status === 'Processed' ? 'bg-success' : 'bg-info'}">${order.status}</span></td>
                </tr>
                <tr>
                    <th>Customer Name</th>
                    <td>${order.customerName}</td>
                </tr>
                <tr>
                    <th>Customer Email</th>
                    <td>${order.customerEmail}</td>
                </tr>
                <tr>
                    <th>Shipping Address</th>
                    <td>${order.shippingAddress}</td>
                </tr>
                <tr>
                    <th>Total Amount</th>
                    <td><strong>$${order.totalAmount.toFixed(2)}</strong></td>
                </tr>
                <tr>
                    <th>Created At</th>
                    <td>${new Date(order.createdAt).toLocaleString()}</td>
                </tr>
                ${order.processedAt ? `
                <tr>
                    <th>Processed At</th>
                    <td>${new Date(order.processedAt).toLocaleString()}</td>
                </tr>
                ` : ''}
            </table>
        </div>
        <div>
            <h6>Order Items</h6>
            <table class="table table-striped">
                <thead>
                    <tr>
                        <th>Product ID</th>
                        <th>Product Name</th>
                        <th>Quantity</th>
                        <th>Unit Price</th>
                        <th>Subtotal</th>
                    </tr>
                </thead>
                <tbody>
                    ${itemsHtml}
                </tbody>
            </table>
        </div>
    `;
    
    modal.show();
}

// Show alert
function showAlert(type, message) {
    const alertContainer = document.getElementById('orderAlert');
    alertContainer.innerHTML = `
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
    
    // Auto dismiss after 5 seconds
    setTimeout(() => {
        const alert = alertContainer.querySelector('.alert');
        if (alert) {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }
    }, 5000);
}

// Get API key (if using APIM)
function getApiKey() {
    // You can store this in localStorage or prompt user
    return localStorage.getItem('apim-subscription-key') || '';
}

// Smooth scroll for navigation links
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    });
});

