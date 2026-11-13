-- Private Cloudflare D1 throttle; hashes are salted and rotate hourly.
CREATE TABLE IF NOT EXISTS bug_rate (
 digest TEXT PRIMARY KEY,
 hour INTEGER NOT NULL,
 count INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS bug_rate_by_hour ON bug_rate(hour);
