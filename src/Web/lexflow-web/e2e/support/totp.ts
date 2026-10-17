import { createHmac } from 'node:crypto';

/** Código TOTP (RFC 6238: HMAC-SHA1, passo de 30 s, 6 dígitos), como um aplicativo autenticador faria. */
export function totp(base32Key: string, at = Date.now()): string {
  const counter = Buffer.alloc(8);
  counter.writeBigInt64BE(BigInt(Math.floor(at / 1000 / 30)));
  const hash = createHmac('sha1', fromBase32(base32Key)).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary = (hash.readUInt32BE(offset) & 0x7fffffff) % 1_000_000;
  return binary.toString().padStart(6, '0');
}

function fromBase32(input: string): Buffer {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = 0;
  let value = 0;
  const bytes: number[] = [];
  for (const char of input.replace(/=+$/, '').toUpperCase()) {
    value = (value << 5) | alphabet.indexOf(char);
    bits += 5;
    if (bits >= 8) {
      bytes.push((value >>> (bits - 8)) & 0xff);
      bits -= 8;
    }
  }

  return Buffer.from(bytes);
}
