const AURA_HISTORY_KEY = 'aura_chat_history';
let chatHistory = JSON.parse(localStorage.getItem(AURA_HISTORY_KEY) || '[]');

function toggleChat() {
    const panel = document.getElementById('aura-chat-panel');
    panel.classList.toggle('d-none');
    if (!panel.classList.contains('d-none')) {
        renderMessages();
        scrollToBottom();
        document.getElementById('aura-chat-input-text').focus();
    }
}

function renderMessages() {
    const container = document.getElementById('aura-chat-messages');
    container.innerHTML = '';
    
    if (chatHistory.length === 0) {
        container.innerHTML = '<div class="text-center text-muted mt-3"><small>Xin chào! Mình là Bé Aura, bạn cần giúp gì không?</small></div>';
    }

    chatHistory.forEach(msg => {
        const div = document.createElement('div');
        div.className = `aura-msg ${msg.role === 'user' ? 'aura-msg-user' : 'aura-msg-bot'}`;
        div.innerText = msg.content;
        container.appendChild(div);
    });
}

function scrollToBottom() {
    const container = document.getElementById('aura-chat-messages');
    container.scrollTop = container.scrollHeight;
}

async function sendChatMessage() {
    const input = document.getElementById('aura-chat-input-text');
    const text = input.value.trim();
    if (!text) return;

    input.value = '';
    input.disabled = true;
    const btn = document.getElementById('aura-chat-send-btn');
    btn.disabled = true;

    // Add user message
    const userMsg = { role: 'user', content: text };
    chatHistory.push(userMsg);
    if (chatHistory.length > 20) chatHistory.shift();
    
    renderMessages();
    scrollToBottom();

    // Add loading indicator
    const container = document.getElementById('aura-chat-messages');
    const loadingDiv = document.createElement('div');
    loadingDiv.className = 'aura-msg aura-msg-bot';
    loadingDiv.id = 'aura-chat-loading';
    loadingDiv.innerHTML = '<i class="fas fa-ellipsis-h fa-beat"></i>';
    container.appendChild(loadingDiv);
    scrollToBottom();

    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                history: chatHistory.slice(0, -1), // Send history without current msg
                message: text
            })
        });

        const result = await response.json();
        
        document.getElementById('aura-chat-loading')?.remove();

        if (result.reply) {
            chatHistory.push({ role: 'assistant', content: result.reply });
            if (chatHistory.length > 20) chatHistory.shift();
        }
        
        localStorage.setItem(AURA_HISTORY_KEY, JSON.stringify(chatHistory));

        if (result.requireLogin) {
            alert('Vui lòng đăng nhập!');
            window.location.href = '/Account/Login';
        }

        renderMessages();
        scrollToBottom();

        if (result.redirectUrl) {
            const redirectBtn = document.createElement('a');
            redirectBtn.className = 'btn btn-sm mt-2';
            redirectBtn.style.backgroundColor = '#1a1a1a';
            redirectBtn.style.color = '#ffffff';
            redirectBtn.href = result.redirectUrl;
            redirectBtn.innerText = 'Mở trang';
            
            const lastBotMsg = container.lastElementChild;
            if (lastBotMsg && lastBotMsg.classList.contains('aura-msg-bot')) {
                lastBotMsg.appendChild(document.createElement('br'));
                lastBotMsg.appendChild(redirectBtn);
            }
        }

    } catch (error) {
        document.getElementById('aura-chat-loading')?.remove();
        console.error('Chat error:', error);
        alert('Đã có lỗi xảy ra, vui lòng thử lại sau!');
    } finally {
        input.disabled = false;
        btn.disabled = false;
        input.focus();
    }
}

function handleChatKeyPress(event) {
    if (event.key === 'Enter') {
        sendChatMessage();
    }
}
