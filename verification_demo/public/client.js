const chatContainer = document.getElementById('chat-container');
const userInput = document.getElementById('user-input');
const sendBtn = document.getElementById('send-btn');

sendBtn.addEventListener('click', sendMessage);
userInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') sendMessage();
});

async function sendMessage() {
    const text = userInput.value.trim();
    if (!text) return;

    appendMessage('user', text);
    userInput.value = '';

    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: text })
        });

        const data = await response.json();
        if (data.error) throw new Error(data.error);

        appendMessage('assistant', data.message);
    } catch (error) {
        console.error(error);
        appendMessage('assistant', 'Error: ' + error.message);
    }
}

function appendMessage(role, text) {
    const div = document.createElement('div');
    div.className = `message ${role}`;

    const textSpan = document.createElement('span');
    textSpan.textContent = text;
    div.appendChild(textSpan);

    if (role === 'assistant') {
        const speakBtn = document.createElement('button');
        speakBtn.className = 'speak-btn';
        speakBtn.textContent = '🔊 Speak';
        speakBtn.onclick = () => playSpeech(text);
        div.appendChild(speakBtn);
    }

    chatContainer.appendChild(div);
    chatContainer.scrollTop = chatContainer.scrollHeight;
}

async function playSpeech(text) {
    try {
        console.log("Requesting speech for:", text);
        const response = await fetch('/api/speech', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text: text })
        });

        if (!response.ok) throw new Error('Speech generation failed');

        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const audio = new Audio(url);
        audio.play();
    } catch (error) {
        console.error("Speech error:", error);
        alert("Failed to generate speech");
    }
}
