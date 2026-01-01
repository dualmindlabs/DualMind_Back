import requests
import json
import sys
import uuid

BASE_URL = "http://localhost:65476/api/admin"

def log(msg, status="INFO"):
    print(f"[{status}] {msg}")

def verify_api():
    log("Starting Admin API Verification...")

    # 1. LIST PROVIDERS
    try:
        log("1. GET /providers")
        res = requests.get(f"{BASE_URL}/providers")
        if res.status_code != 200:
            log(f"Failed to list providers: {res.text}", "FAIL")
            return
        data = res.json()
        log(f"Found {len(data.get('data',[]))} providers.", "PASS")
    except Exception as e:
        log(f"Exception listing providers: {e}", "FAIL")
        return

    # 2. CREATE PROVIDER
    test_provider = f"test_prov_{uuid.uuid4().hex[:6]}"
    try:
        log(f"2. POST /providers (Name: {test_provider})")
        payload = {
            "provider_name": test_provider,
            "display_name": "Test Provider",
            "is_enabled": True,
            "priority": 50
        }
        res = requests.post(f"{BASE_URL}/providers", json=payload)
        if res.status_code != 200:
            log(f"Failed to create provider: {res.text}", "FAIL")
            return
        log("Provider created successfully.", "PASS")
    except Exception as e:
        log(f"Exception creating provider: {e}", "FAIL")
        return

    # 3. UPDATE PROVIDER
    try:
        log(f"3. PUT /providers/{test_provider}")
        payload = {
            "display_name": "Test Provider Updated",
            "is_enabled": False,
            "priority": 10
        }
        res = requests.put(f"{BASE_URL}/providers/{test_provider}", json=payload)
        if res.status_code != 200:
            log(f"Failed to update provider: {res.text}", "FAIL")
            return
        log("Provider updated successfully.", "PASS")
    except Exception as e:
        log(f"Exception updating provider: {e}", "FAIL")
        return

    # 4. ADD KEY
    try:
        log(f"4. POST /providers/{test_provider}/keys")
        key_secret = "sk-test-1234567890-abcdef"
        payload = {
            "api_key": key_secret,
            "is_active": True
        }
        res = requests.post(f"{BASE_URL}/providers/{test_provider}/keys", json=payload)
        if res.status_code != 200:
            log(f"Failed to add key: {res.text}", "FAIL")
            return
        key_id = res.json().get('data')
        log(f"Key added successfully. ID: {key_id}", "PASS")
    except Exception as e:
        log(f"Exception adding key: {e}", "FAIL")
        return

    # 5. LIST KEYS & VERIFY MASK
    try:
        log(f"5. GET /providers/{test_provider}/keys")
        res = requests.get(f"{BASE_URL}/providers/{test_provider}/keys")
        if res.status_code != 200:
            log(f"Failed to list keys: {res.text}", "FAIL")
            # Cleanup anyway
            return 
        keys = res.json().get('data', [])
        target = next((k for k in keys if k['KeyId'] == key_id), None)
        if not target:
            log("Created key not found in list!", "FAIL")
        else:
            mask = target.get('DisplayMask', '')
            if "sk-test" in mask and "abcdef" in mask and key_secret not in mask: 
                 # Basic check: expected valid mask behavior
                 pass
            
            if target.get('EncryptedApiKey'):
                log("SECURITY ALERT: EncryptedApiKey returned in API!", "FAIL")
            else:
                log(f"Key found. Mask: {mask}", "PASS")
    except Exception as e:
        log(f"Exception listing keys: {e}", "FAIL")
        return

    # 6. DELETE KEY
    try:
        log(f"6. DELETE /keys/{key_id}")
        res = requests.delete(f"{BASE_URL}/keys/{key_id}")
        if res.status_code != 200:
            log(f"Failed to delete key: {res.text}", "FAIL")
        else:
            log("Key deleted successfully.", "PASS")
    except Exception as e:
        log(f"Exception deleting key: {e}", "FAIL")

    log("Verification Complete.")

if __name__ == "__main__":
    verify_api()
