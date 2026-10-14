import { Router } from 'express';
import { z } from 'zod';
import { AuthController } from '../controllers/auth.controller';
import { validateBody } from '../middleware/validate.middleware';
import { authMiddleware } from '../middleware/auth.middleware';

const router = Router();

const loginSchema = z.object({
  email: z.string().email('Valid email is required'),
  password: z.string().min(1, 'Password is required'),
});

// POST /api/auth/login - Placement staff authentication
router.post('/login', validateBody(loginSchema), AuthController.login);

// GET /api/auth/me - Current staff profile
router.get('/me', authMiddleware, AuthController.me);

export default router;
