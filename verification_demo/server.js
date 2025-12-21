require('dotenv').config({ path: '../.env' });
const express = require('express');
const cors = require('cors');
const fetch = require('node-fetch');

const app = express();
const port = 3000;

app.use(cors());
app.use(express.json());
app.use(express.static('public'));

const GROQ_API_KEY = process.env.GROQ_API_KEY;

if (!GROQ_API_KEY) {
    console.error("GROQ_API_KEY not found in environment!");
}

app.post('/api/chat', async (req, res) => {
    try {
        const { message } = req.body;

        const response = await fetch("https://api.groq.com/openai/v1/chat/completions", {
            method: "POST",
            headers: {
                "Authorization": `Bearer ${GROQ_API_KEY}`,
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                model: "llama-3.3-70b-versatile",
                messages: [{ role: "user", content: message }],
                max_tokens: 1024
            })
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Groq API error: ${response.status} - ${errorText}`);
        }

        const data = await response.json();
        const botMessage = data.choices[0].message.content;
        res.json({ message: botMessage });

    } catch (error) {
        console.error("Chat error:", error);
        res.status(500).json({ error: error.message });
    }
});

app.post('/api/speech', async (req, res) => {
    try {
        const { text, voice } = req.body;

        const response = await fetch("https://api.groq.com/openai/v1/audio/speech", {
            method: "POST",
            headers: {
                "Authorization": `Bearer ${GROQ_API_KEY}`,
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                model: "playai-tts",
                input: text,
                voice: voice || "Celeste-PlayAI",
                response_format: "wav"
            })
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Groq Speech API error: ${response.status} - ${errorText}`);
        }

        const buffer = await response.buffer();
        res.set('Content-Type', 'audio/wav');
        res.send(buffer);

    } catch (error) {
        console.error("Speech error:", error);
        res.status(500).json({ error: error.message });
    }
});

app.listen(port, () => {
    console.log(`Server running at http://localhost:${port}`);
});
