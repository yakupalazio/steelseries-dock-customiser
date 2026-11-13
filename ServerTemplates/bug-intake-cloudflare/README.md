# Private bug-report intake — optional server component

This folder is **private developer source only**, never a file in the public Windows ZIP. Nothing here is deployed or enabled automatically. The app's Bug Report form saves unsent local drafts until a real HTTPS endpoint is configured. The ZIP contains no GitHub secret and no server keys.

## Operator setup (requires Cloudflare account and a private GitHub Issues repository)

1. Provision a Cloudflare Worker, D1 database, and an **access-restricted, private** R2 bucket; copy `wrangler.example.toml` to `wrangler.toml`, fill your own IDs. Apply `schema.sql` to D1.
2. Create a GitHub fine-grained PAT (or GitHub App installation token) restricted to **Issues: write** on the private issues-only repository. Never store it in a client EXE, repo, README or Worker source. Use Wrangler secrets `GH_TOKEN` and `HASH_SALT`.
3. Add **Cloudflare WAF/rate-limiting controls** to restrict distributed abuse before public launch. Worker also applies an approximate 5-reports-per-IP-per-hour D1 counter using salted IP hashes. Regularly expire old counter rows and R2 attachments under a written retention policy. Do not turn off WAF based on the in-worker cap alone.
4. Deploy the worker to a domain you control with HTTPS, e.g. `https://reports.example.invalid/api/bugs`. Then create `%LOCALAPPDATA%\\SteelseriesDockCustomiser-Beta5-Development\\bug-report-endpoint-v1.json` locally with:

```json
{
  "Endpoint": "https://your-real-domain.example/api/bugs"
}
```

5. Send an explicit opt-in test report, verify the response in the app and the resulting issue in the private repository. Check unauthorized input, high-frequency submission, attachment permissions, and token rotation. Only then enable distribution of a preconfigured endpoint in a future production build.

## Security and workshop separation

- The endpoint has no public GitHub token. Private R2 screenshots are NOT served by public URLs. Their keys appear only inside private Issues.
- The Windows app sends only what the user explicitly enters and approves, and strips local screenshot file names. Do not auto-upload diagnostic logs or profile files.
- This service is NOT a Workshop or gallery backend. It does not accept designs, GIFs, user credentials or arbitrary executable uploads.
- The future Workshop should have its own separate security boundaries, moderation queue, versioned data-only profiles and asset validation. Never reuse this bug-intake API for public design uploads.
- This code is a deployable **starting template**, not a hosted production service. A security review, operations policy and abuse testing are still necessary before public use.
