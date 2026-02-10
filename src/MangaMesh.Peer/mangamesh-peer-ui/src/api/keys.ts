import type { KeyPair, KeyChallenge, VerifySignatureResponse } from '../types/api';

const AUTH_API_BASE = '/api/keys';
const CRYPTO_SERVICE_BASE = '/api/keys';

export async function getKeys(): Promise<KeyPair> {
    try {
        const response = await fetch('/api/keys');
        if (!response.ok) throw new Error(response.statusText);
        return await response.json();
    } catch (e) {
        console.warn('API unavailable, returning empty keys', e);
        return { publicKeyBase64: '', privateKeyBase64: '' };
    }
}

export async function generateKeys(): Promise<KeyPair> {
    try {
        const response = await fetch('/api/keys/generate', { method: 'POST' });
        if (!response.ok) throw new Error(response.statusText);
        return await response.json();
    } catch (e) {
        console.warn('API unavailable, returning empty keys', e);
        return { publicKeyBase64: '', privateKeyBase64: '' };
    }
}

export async function requestChallenge(publicKeyBase64: string): Promise<KeyChallenge> {
    const response = await fetch(`${AUTH_API_BASE}/challenges`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ publicKey: publicKeyBase64 })
    });
    if (!response.ok) throw new Error(`Failed to request challenge: ${response.statusText}`);
    return await response.json();
}

export async function solveChallenge(nonceBase64: string, privateKeyBase64: string): Promise<string> {
    const response = await fetch(`${CRYPTO_SERVICE_BASE}/challenge/solve`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nonceBase64, privateKeyBase64 })
    });
    if (!response.ok) throw new Error(`Failed to solve challenge: ${response.statusText}`);
    return await response.json();
}

export async function verifySignature(publicKeyBase64: string, challengeId: string, signatureBase64: string): Promise<VerifySignatureResponse> {
    const response = await fetch(`${AUTH_API_BASE}/challenges/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ challengeId, signatureBase64, publicKey: publicKeyBase64 })
    });
    if (!response.ok) throw new Error(`Failed to verify signature: ${response.statusText}`);
    return await response.json();
}
