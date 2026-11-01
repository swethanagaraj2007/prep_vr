import bcrypt from 'bcryptjs';
import jwt from 'jsonwebtoken';
import prisma from '../prisma/client';
import { AppError } from '../middleware/errorHandler.middleware';

export class AuthService {
  /**
   * Placement Staff Login
   */
  static async login(email: string, password: string) {
    const emailClean = (email || '').trim().toLowerCase();
    const adminEmail = (process.env.PLACEMENT_ADMIN_EMAIL || 'admin@prepvr.edu').trim().toLowerCase();
    const adminPassword = process.env.PLACEMENT_ADMIN_PASSWORD || 'Placement@2026';

    // 1. Check PlacementUser table in DB
    let user = await prisma.placementUser.findUnique({
      where: { email: emailClean },
    });

    let isValid = false;

    if (user) {
      isValid = await bcrypt.compare(password, user.passwordHash);
    } else if (emailClean === adminEmail) {
      // Fallback: Default demo admin matches environment configuration
      isValid = password === adminPassword;
      if (isValid) {
        // Auto-provision demo admin in database
        const hash = await bcrypt.hash(adminPassword, 10);
        user = await prisma.placementUser.create({
          data: {
            email: adminEmail,
            name: 'Placement Directorate',
            passwordHash: hash,
            role: 'DIRECTOR_OF_PLACEMENTS',
          },
        });
      }
    }

    if (!isValid || !user) {
      throw new AppError('Invalid placement credentials. Check email and password.', 401);
    }

    const secret = process.env.JWT_SECRET || 'prepvr_super_secret_jwt_key_placement_2026';
    const token = jwt.sign(
      {
        id: user.id,
        email: user.email,
        name: user.name,
        role: user.role,
      },
      secret,
      { expiresIn: '7d' }
    );

    return {
      token,
      user: {
        id: user.id,
        email: user.email,
        name: user.name,
        role: user.role,
      },
    };
  }
}
