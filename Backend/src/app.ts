import express from 'express';
import cors from 'cors';
import path from 'path';
import fs from 'fs';
import studentRoutes from './routes/student.routes';
import interviewRoutes from './routes/interview.routes';
import dashboardRoutes from './routes/dashboard.routes';
import rankingRoutes from './routes/ranking.routes';
import authRoutes from './routes/auth.routes';
import reportRoutes from './routes/report.routes';
import { errorHandler } from './middleware/errorHandler.middleware';

export function createApp() {
  const app = express();

  // CORS configuration for Placement Dashboard and Unity WebRequests
  const allowedOrigins = (process.env.CORS_ORIGIN || 'http://localhost:3000,http://127.0.0.1:3000')
    .split(',')
    .map((o) => o.trim());

  app.use(
    cors({
      origin: (origin, callback) => {
        // Allow requests with no origin (like Unity desktop apps, mobile apps, or curl)
        if (!origin) return callback(null, true);
        if (allowedOrigins.indexOf(origin) !== -1 || allowedOrigins.includes('*')) {
          return callback(null, true);
        }
        return callback(null, true); // Permissive for local college LAN / demo
      },
      credentials: true,
      methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS', 'PATCH'],
      allowedHeaders: ['Content-Type', 'Authorization', 'X-Requested-With'],
    })
  );

  // Body parser
  app.use(express.json({ limit: '10mb' }));
  app.use(express.urlencoded({ extended: true, limit: '10mb' }));

  // Static directory for reports (if available)
  const reportsDir = path.resolve(process.env.REPORTS_STORAGE_PATH || './reports');
  if (!fs.existsSync(reportsDir)) {
    fs.mkdirSync(reportsDir, { recursive: true });
  }
  app.use('/reports', express.static(reportsDir));

  // Health check endpoint
  app.get('/api/health', (req, res) => {
    res.status(200).json({
      status: 'healthy',
      service: 'PREP VR Centralized Placement Backend',
      timestamp: new Date().toISOString(),
    });
  });

  // Mount API Routes
  app.use('/api/students', studentRoutes);
  app.use('/api/interviews', interviewRoutes);
  app.use('/api/dashboard', dashboardRoutes);
  app.use('/api/rankings', rankingRoutes);
  app.use('/api/auth', authRoutes);
  app.use('/api/reports', reportRoutes);

  // Fallback 404 handler
  app.use('/api/*', (req, res) => {
    res.status(404).json({
      success: false,
      error: `API route not found: ${req.method} ${req.originalUrl}`,
    });
  });

  // Global Error Handler
  app.use(errorHandler);

  return app;
}

export default createApp;
