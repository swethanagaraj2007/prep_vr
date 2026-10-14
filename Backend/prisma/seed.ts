import { PrismaClient } from '@prisma/client';
import bcrypt from 'bcryptjs';

const prisma = new PrismaClient();

async function main() {
  console.log('--- INITIALIZING PREP VR CENTRALIZED PLACEMENT SYSTEM ---');

  // Create or update Placement Admin Staff account
  const adminEmail = (process.env.PLACEMENT_ADMIN_EMAIL || 'admin@prepvr.edu').toLowerCase();
  const adminPassword = process.env.PLACEMENT_ADMIN_PASSWORD || 'Placement@2026';
  const passwordHash = await bcrypt.hash(adminPassword, 10);

  const admin = await prisma.placementUser.upsert({
    where: { email: adminEmail },
    update: { passwordHash, name: 'Dr. V. Ramanathan (Placement Directorate)' },
    create: {
      email: adminEmail,
      name: 'Dr. V. Ramanathan (Placement Directorate)',
      passwordHash,
      role: 'DIRECTOR_OF_PLACEMENTS',
    },
  });
  console.log(`[Init] Placement Staff User ready: ${admin.email}`);
  console.log('[Init] No dummy student records created. Portal will be populated exclusively from live Unity VR interview submissions.');
}

main()
  .catch((e) => {
    console.error('[Init] Error during initialization:', e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
