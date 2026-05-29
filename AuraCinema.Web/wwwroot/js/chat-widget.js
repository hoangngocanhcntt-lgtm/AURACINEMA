const AURA_HISTORY_KEY = 'aura_chat_history';
let chatHistory = JSON.parse(localStorage.getItem(AURA_HISTORY_KEY) || '[]');
let isSending = false;
let lastSendTime = 0;
let currentAbort = null;
const COOLDOWN_MS = 3000; // 3 giây cooldown giữa các tin nhắn

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

    // Chặn spam: đang gửi hoặc chưa hết cooldown
    const now = Date.now();
    if (isSending) return;
    if (now - lastSendTime < COOLDOWN_MS) {
        const remaining = Math.ceil((COOLDOWN_MS - (now - lastSendTime)) / 1000);
        return;
    }

    // Cancel request cũ nếu còn pending
    if (currentAbort) {
        currentAbort.abort();
        currentAbort = null;
    }

    isSending = true;
    lastSendTime = now;
    input.value = '';
    input.disabled = true;
    const btn = document.getElementById('aura-chat-send-btn');
    btn.disabled = true;

    // Add user message
    const userMsg = { role: 'user', content: text };
    chatHistory.push(userMsg);
    if (chatHistory.length > 10) chatHistory.shift();
    
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
        currentAbort = new AbortController();
        // Chỉ gửi tối đa 4 tin nhắn history gần nhất lên server
        const historyToSend = chatHistory.slice(0, -1).slice(-4);
        
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                history: historyToSend,
                message: text
            }),
            signal: currentAbort.signal
        });

        const result = await response.json();
        currentAbort = null;
        
        document.getElementById('aura-chat-loading')?.remove();

        if (result.reply) {
            chatHistory.push({ role: 'assistant', content: result.reply });
            if (chatHistory.length > 10) chatHistory.shift();
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
        currentAbort = null;
        document.getElementById('aura-chat-loading')?.remove();
        if (error.name === 'AbortError') return; // User cancelled, không hiện lỗi
        console.error('Chat error:', error);
        chatHistory.push({ role: 'assistant', content: 'Đã có lỗi xảy ra, vui lòng thử lại sau!' });
        renderMessages();
        scrollToBottom();
    } finally {
        isSending = false;
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
