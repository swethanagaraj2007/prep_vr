import prisma from '../src/prisma/client';

async function clearData() {
  console.log('[PREP VR] Cleaning dummy demo data from database...');

  // Delete all feedback, stages, interviews, and demo students
  const deletedFeedback = await prisma.feedback.deleteMany({});
  console.log(`Deleted ${deletedFeedback.count} feedback records.`);

  const deletedStages = await prisma.interviewStage.deleteMany({});
  console.log(`Deleted ${deletedStages.count} interview stages.`);

  const deletedInterviews = await prisma.interview.deleteMany({});
  console.log(`Deleted ${deletedInterviews.count} interview records.`);

  const deletedStudents = await prisma.student.deleteMany({});
  console.log(`Deleted ${deletedStudents.count} student records.`);

  console.log('Database is now completely clean of dummy data.');
  console.log('Only genuine Unity VR mock interview sessions will be populated!');
}

clearData()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
