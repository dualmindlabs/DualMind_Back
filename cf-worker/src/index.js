/**
 * DualMind API — Cloudflare Worker Entry Point
 *
 * This Worker orchestrates the DualMind ASP.NET Core container:
 * - Routes all HTTP requests across multiple container instances (getRandom)
 * - Keeps ALL instances warm via cron trigger (every 5 min)
 * - Auto-sets Telegram webhook URL on deploy
 *
 * INSTANCE_COUNT must match max_instances in wrangler.jsonc.
 * Change this number to scale up/down the number of live containers.
 */

import { Container, getContainer } from "@cloudflare/containers";

// ---------------------------------------------------------------------------
// How many container instances to spread load across.
// Must match "max_instances" in wrangler.jsonc.
// ---------------------------------------------------------------------------
const INSTANCE_COUNT = 3;

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
   * Handle incoming HTTP requests.
   * Distributes load randomly across INSTANCE_COUNT containers.
   * Each container instance gets a unique numeric ID (0 to INSTANCE_COUNT-1).
   */
  async fetch(request, env, ctx) {
    // Generate a random instance ID between 0 and (INSTANCE_COUNT - 1)
    const instanceId = Math.floor(Math.random() * INSTANCE_COUNT).toString();
    const container = getContainer(env.DUALMIND_CONTAINER, instanceId);

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

    // Forward the request to the selected container instance
    return container.fetch(request);
  },

  /**
   * Cron Trigger — lightweight heartbeat every 5 minutes.
   * Only keeps the Worker itself alive, NOT pre-warming container instances.
   * Container instances are created lazily on demand under load.
   */
  async scheduled(controller, env, ctx) {
    console.log(`[heartbeat] Worker alive at ${new Date().toISOString()}`);
  },
};
