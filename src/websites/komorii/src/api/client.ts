import axios from 'axios';

// Use relative path to leverage Vite proxy in development
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '';

const client = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

export default client;
