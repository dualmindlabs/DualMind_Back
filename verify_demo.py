from playwright.sync_api import sync_playwright, expect
import time

def verify_speech_demo():
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()

        # 1. Navigate to the demo page
        print("Navigating to http://localhost:3000...")
        page.goto("http://localhost:3000")

        # 2. Check initial state
        expect(page.get_by_role("heading", name="Groq Text-to-Speech Demo")).to_be_visible()

        # 3. Type a message
        print("Typing message...")
        page.fill("#user-input", "Hello Groq!")
        page.click("#send-btn")

        # 4. Wait for the assistant message to appear
        # The assistant message container has class 'assistant'
        print("Waiting for response...")
        assistant_message = page.locator(".message.assistant").first
        expect(assistant_message).to_be_visible(timeout=10000)

        # 5. Check for the "Speak" button
        print("Checking for Speak button...")
        speak_btn = assistant_message.locator("button.speak-btn")
        expect(speak_btn).to_be_visible()

        # 6. Click the Speak button
        # We need to intercept the network request to verify it calls the correct endpoint
        with page.expect_response("**/api/speech") as response_info:
            print("Clicking Speak button...")
            speak_btn.click()

        response = response_info.value
        print(f"Speech API Response: {response.status}")

        if response.status == 200:
            print("Speech generation successful!")
        else:
            print(f"Speech generation failed with status {response.status}")

        # 7. Take a screenshot
        time.sleep(1) # wait for any UI updates
        page.screenshot(path="verification_demo.png")
        print("Screenshot saved to verification_demo.png")

        browser.close()

if __name__ == "__main__":
    verify_speech_demo()
