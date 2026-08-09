/**
 * DualMind API — Cloudflare Worker Entry Point
 * 
 * This Worker orchestrates the DualMind ASP.NET Core container:
 * - Routes all HTTP requests to the container
 * - Keeps the container warm via cron trigger (every 5 min)
 * - Auto-sets Telegram webhook URL on deploy
 */

import { Container, getContainer } from "@cloudflare/containers";

// ---------------------------------------------------------------------------
// Container Definition
// ---------------------------------------------------------------------------
export class DualMindContainer extends Container {
  defaultPort = 8080;
  sleepAfter = "120m"; // Enterprise: keep warm for 2 hours after last request
}

// ---------------------------------------------------------------------------
// Telegram Webhook Auto-Configuration
// ---------------------------------------------------------------------------
async function ensureTelegramWebhook(env, workerUrl) {
  const botToken = env.TELEGRAM_BOT_TOKEN;
  if (!botToken) {
    console.log("TELEGRAM_BOT_TOKEN not set, skipping webhook setup");
    return;
  }

  const webhookUrl = `${workerUrl}/api/telegram/webhook`;
  
  try {
    // Check current webhook
    const infoRes = await fetch(
      `https://api.telegram.org/bot${botToken}/getWebhookInfo`
    );
    const info = await infoRes.json();
    
    if (info.result?.url === webhookUrl) {
      console.log(`Telegram webhook already set to ${webhookUrl}`);
      return;
    }

    // Set new webhook
    const setRes = await fetch(
      `https://api.telegram.org/bot${botToken}/setWebhook`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          url: webhookUrl,
          allowed_updates: ["message", "callback_query"],
          drop_pending_updates: false,
        }),
      }
    );
    const result = await setRes.json();
    console.log(`Telegram webhook set: ${JSON.stringify(result)}`);
  } catch (err) {
    console.error("Failed to set Telegram webhook:", err);
  }
}

// ---------------------------------------------------------------------------
// Track whether webhook has been configured this instance
// ---------------------------------------------------------------------------
let webhookConfigured = false;

// ---------------------------------------------------------------------------
// Main Worker Export
// ---------------------------------------------------------------------------
export default {
  /**
   * Handle incoming HTTP requests — forward everything to the container.
   */
  async fetch(request, env, ctx) {
    const container = getContainer(env.DUALMIND_CONTAINER, "default");

    // Auto-configure Telegram webhook on first request after deploy
    if (!webhookConfigured) {
      const url = new URL(request.url);
      const workerUrl = `${url.protocol}//${url.host}`;
      ctx.waitUntil(
        ensureTelegramWebhook(env, workerUrl).then(() => {
          webhookConfigured = true;
        })
      );
    }

    // Forward the request to the ASP.NET Core container
    return container.fetch(request);
  },

  /**
   * Cron Trigger — runs every 5 minutes to keep the container warm.
   * Prevents cold starts by ensuring the container never goes to sleep.
   */
  async scheduled(controller, env, ctx) {
    const container = getContainer(env.DUALMIND_CONTAINER, "default");

    ctx.waitUntil(
      (async () => {
        try {
          const response = await container.fetch(
            new Request("http://internal/health")
          );
          console.log(
            `[keep-alive] Container health: ${response.status} at ${new Date().toISOString()}`
          );
        } catch (err) {
          console.error("[keep-alive] Container ping failed:", err);
        }
      })()
    );
  },
};
