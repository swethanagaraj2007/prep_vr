import dotenv from 'dotenv';
dotenv.config();

import { createApp } from './app';
import prisma from './prisma/client';

const PORT = parseInt(process.env.PORT || '5000', 10);
const HOST = '0.0.0.0';
const app = createApp();

const server = app.listen(PORT, HOST, async () => {
  console.log(`====================================================`);
  console.log(` PREP VR — Centralized Placement Data API Server`);
  console.log(` Status: Running`);
  console.log(` Listening on: http://localhost:${PORT} (0.0.0.0:${PORT})`);
  console.log(` Health Check: http://localhost:${PORT}/api/health`);
  console.log(` Dashboard Summary: http://localhost:${PORT}/api/dashboard/summary`);
  console.log(` Student Rankings: http://localhost:${PORT}/api/rankings`);
  console.log(`====================================================`);

  try {
    await prisma.$connect();
    console.log(`[Database] Successfully connected to PostgreSQL via Prisma.`);
  } catch (err: any) {
    console.error(`[Database] Connection warning: ${err.message}`);
  }
});

process.on('SIGTERM', async () => {
  console.log('SIGTERM signal received. Closing HTTP server.');
  server.close(async () => {
    await prisma.$disconnect();
    process.exit(0);
  });
});
