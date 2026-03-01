import requests
import json
import random
import uuid
from datetime import datetime, timedelta
import time

import os

SUPABASE_URL = "https://calqfzajyidkdzbaswjp.supabase.co"
SUPABASE_SERVICE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImNhbHFmemFqeWlka2R6YmFzd2pwIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc2NDI3MzA4MywiZXhwIjoyMDc5ODQ5MDgzfQ.bt3MjR2dItU1FT3yRTlNkNhNPRFO5_NBO1lMCqQy1d8"

headers = {
    "apikey": SUPABASE_SERVICE_KEY,
    "Authorization": f"Bearer {SUPABASE_SERVICE_KEY}",
    "Content-Type": "application/json",
    "Prefer": "return=minimal"
}

def get_models():
    response = requests.get(f"{SUPABASE_URL}/rest/v1/ai_models?status=eq.active", headers=headers)
    data = response.json()
    print("API Response:", data)
    return data

def seed_data():
    models = get_models()
    print(f"Found {len(models)} active models:")
    for m in models:
        print(f" - {m['model_name']} ({m['display_name']})")

    if len(models) < 2:
        print("Not enough models to create comparisons. Please add more models first.")
        return

    # Ensure we use an existing user from the auth database.
    # In Supabase, usually we don't manually create users in the public.users schema unless
    # there is a corresponding auth.users.
    # To keep it simple and clean, let's allow null user_ids (anonymous battles)
    # or fetch existing users if any.
    print("Fetching existing users...")
    response = requests.get(f"{SUPABASE_URL}/rest/v1/users?select=user_id", headers=headers)
    users_data = response.json()
    user_ids = [u['user_id'] for u in users_data]

    if not user_ids:
        print("No existing users found. Battles will be anonymous (user_id = null).")
        user_ids = [None]
    else:
        print(f"Found {len(user_ids)} users.")

    prompts = [
        "Write a Python script to scrape a website.", "Explain quantum computing to a 5-year-old.",
        "Tell me a short joke about programmers.", "Write a 500-word essay on the Roman Empire.",
        "How do I fix a NullReferenceException in C#?", "Write a movie script about a rogue AI.",
        "Translate 'Hello, how are you?' to Japanese.", "What is the capital of France?",
        "Write a short story about a time traveler.", "Give me a recipe for chocolate chip cookies.",
        "Explain the difference between TCP and UDP.", "Write a React component for a login form.",
        "What are the best practices for REST API design?", "How does a blockchain work?",
        "Write a haiku about autumn.", "What is the meaning of life?",
        "Debug this SQL query: SELECT * FROM users WHERE age = 'twenty'",
        "Write a cover letter for a software engineer position.", "Explain the theory of relativity.",
        "What are the health benefits of green tea?", "Write a poem about the ocean.",
        "How do I invest in stocks?", "What is the history of the internet?",
        "Write a summary of '1984' by George Orwell.", "Explain how a combustion engine works."
    ]

    responses = [
        "Here is the detailed step-by-step solution you requested. First, we need to consider...",
        "I can help with that. Here is a concise answer:\n\n1. Point A\n2. Point B\n3. Point C",
        "Sure thing! Here's a creative take on your prompt...",
        "```python\ndef solution():\n    print('Hello World')\n```",
        "The answer is quite complex. It depends on several factors...",
        "I'm sorry, I don't have enough context to answer that fully, but generally speaking...",
        "Here is a comprehensive overview of the topic, including historical context and modern applications.",
        "That's a great question! Based on my knowledge base...",
        "Error: Unable to process request due to complex constraints.",
        "Here's a quick and simple explanation..."
    ]

    print("Generating comparisons, threads, and votes...")
    total_comparisons = 450
    chunk_size = 50

    for chunk_start in range(0, total_comparisons, chunk_size):
        chunk_end = min(chunk_start + chunk_size, total_comparisons)
        print(f"Processing chunk {chunk_start + 1} to {chunk_end}...")

        comparisons_data = []
        threads_data = []
        messages_data = []
        votes_data = []

        for i in range(chunk_start, chunk_end):
            # Select random user and models
            user_id = random.choice(user_ids)
            model1, model2 = random.sample(models, 2)
            prompt = random.choice(prompts)

            created_at = (datetime.utcnow() - timedelta(days=random.randint(0, 30), hours=random.randint(0, 24))).isoformat()

            # Create Thread
            thread_id = str(uuid.uuid4())
            threads_data.append({
                "thread_id": thread_id,
                "user_id": user_id,
                "title": prompt[:50] + "...",
                "mode": "arena",
                "visibility": random.choice(["public", "private", "unlisted"]),
                "message_count": 1,
                "created_at": created_at,
                "updated_at": created_at
            })

            # Create Comparison
            comparison_id = str(uuid.uuid4())
            model1_time = random.randint(500, 5000)
            model2_time = random.randint(500, 5000)

            # Create Responses (vary length to make it realistic)
            resp1 = random.choice(responses) * random.randint(1, 5)
            resp2 = random.choice(responses) * random.randint(1, 5)

            comparisons_data.append({
                "comparison_id": comparison_id,
                "user_id": user_id,
                "prompt_text": prompt,
                "model1_id": model1["model_id"],
                "model2_id": model2["model_id"],
                "model1_response": resp1,
                "model2_response": resp2,
                "model1_time_ms": model1_time,
                "model2_time_ms": model2_time,
                "is_revealed": True,
                "created_at": created_at
            })

            # Create Thread Message
            message_id = str(uuid.uuid4())
            messages_data.append({
                "message_id": message_id,
                "thread_id": thread_id,
                "comparison_id": comparison_id,
                "prompt_text": prompt,
                "model1_name": model1["model_name"],
                "model2_name": model2["model_name"],
                "model1_response": resp1,
                "model2_response": resp2,
                "model1_time_ms": model1_time,
                "model2_time_ms": model2_time,
                "position": 1,
                "created_at": created_at
            })

            # Create Vote (simulate some ties/both bad)
            vote_choice = random.choices(
                ["left", "right", "tie", "both-bad"],
                weights=[0.4, 0.4, 0.1, 0.1]
            )[0]

            winner_model_id = None
            if vote_choice == "left":
                winner_model_id = model1["model_id"]
            elif vote_choice == "right":
                winner_model_id = model2["model_id"]

            voted_at = (datetime.fromisoformat(created_at) + timedelta(seconds=random.randint(10, 300))).isoformat()

            votes_data.append({
                "vote_id": str(uuid.uuid4()),
                "user_id": user_id,
                "comparison_id": comparison_id,
                "winner_model_id": winner_model_id,
                "vote_choice": vote_choice,
                "vote_duration_ms": random.randint(5000, 60000),
                "voted_at": voted_at,
                "revealed_at": voted_at
            })

        # Insert Data in chunks
        requests.post(f"{SUPABASE_URL}/rest/v1/threads", headers=headers, json=threads_data)
        requests.post(f"{SUPABASE_URL}/rest/v1/comparisons", headers=headers, json=comparisons_data)
        requests.post(f"{SUPABASE_URL}/rest/v1/thread_messages", headers=headers, json=messages_data)
        requests.post(f"{SUPABASE_URL}/rest/v1/model_votes", headers=headers, json=votes_data)

        time.sleep(1) # small pause to not overwhelm Supabase

    print("Data generation complete!")

if __name__ == "__main__":
    seed_data()