using Microsoft.EntityFrameworkCore;
using TimeEvidence.Models;

namespace TimeEvidence.Data
{
    public class TimeEvidenceDbContext : DbContext
    {
        public TimeEvidenceDbContext(DbContextOptions<TimeEvidenceDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Supervisor> Supervisors { get; set; } = null!;
        public DbSet<WorkSchedule> WorkSchedules { get; set; } = null!;
        public DbSet<TimeTrackerData> TimeTrackerData { get; set; } = null!;
        public DbSet<CardAssignment> CardAssignments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Employee configuration
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Surname).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Position).HasMaxLength(50);
                entity.Property(e => e.CardId).HasMaxLength(20);
                entity.HasIndex(e => e.CardId).IsUnique();

                // Foreign key relationships mapped to navigation collections to avoid shadow FKs
                entity.HasOne(e => e.Supervisor)
                    .WithMany(s => s.Employees)
                    .HasForeignKey(e => e.SupervisorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.WorkSchedule)
                    .WithMany(ws => ws.Employees)
                    .HasForeignKey(e => e.WorkScheduleId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Supervisor configuration
            modelBuilder.Entity<Supervisor>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(50);
                entity.Property(s => s.Surname).IsRequired().HasMaxLength(50);
                entity.Property(s => s.Position).HasMaxLength(50);
                entity.Property(s => s.Email).IsRequired().HasMaxLength(100);
                entity.Property(s => s.PhoneNumber).HasMaxLength(30);
                entity.Property(s => s.NotificationPreference)
                      .HasConversion<int>()
                      .HasDefaultValue(NotificationPreference.None);
                entity.HasIndex(s => s.Email).IsUnique();
            });

            // WorkSchedule configuration
            modelBuilder.Entity<WorkSchedule>(entity =>
            {
                entity.HasKey(w => w.Id);
                entity.Property(w => w.ScheduleName).IsRequired().HasMaxLength(100);
                entity.Property(w => w.SelectedDays).IsRequired();
                entity.Property(w => w.TimeRanges).IsRequired();
            });

            // TimeTrackerData configuration
            modelBuilder.Entity<TimeTrackerData>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.SystemId).HasMaxLength(50);
                entity.Property(t => t.Action).HasMaxLength(20);
                entity.Property(t => t.CardId).HasMaxLength(20);
                entity.Property(t => t.Status).HasMaxLength(20);
                entity.Property(t => t.TimestampLocal).HasMaxLength(20);
                entity.Property(t => t.EmployeeName).HasMaxLength(100);
                entity.Property(t => t.AccessLevel).HasMaxLength(20);
                entity.HasIndex(t => t.CardId);
                entity.HasIndex(t => t.ReceivedTimestamp);
            });

            // CardAssignment configuration (for tracking card assignment history)
            modelBuilder.Entity<CardAssignment>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CardId).IsRequired().HasMaxLength(20);
                entity.Property(c => c.EmployeeId).IsRequired();
                entity.Property(c => c.AssignedBy).HasMaxLength(50);
                entity.HasIndex(c => c.CardId);
                entity.HasIndex(c => c.EmployeeId);

                entity.HasOne(c => c.Employee)
                      .WithMany()
                      .HasForeignKey(c => c.EmployeeId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed some initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed default supervisors
            var supervisor1Id = Guid.NewGuid();
            var supervisor2Id = Guid.NewGuid();

            modelBuilder.Entity<Supervisor>().HasData(
                new Supervisor
                {
                    Id = supervisor1Id,
                    Name = "John",
                    Surname = "Manager",
                    Position = "Team Lead",
                    Email = "john.manager@company.com",
                    PhoneNumber = "+1555000111",
                    NotificationPreference = NotificationPreference.Email,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Supervisor
                {
                    Id = supervisor2Id,
                    Name = "Sarah",
                    Surname = "Director",
                    Position = "Operations Director",
                    Email = "sarah.director@company.com",
                    PhoneNumber = "+1555000222",
                    NotificationPreference = NotificationPreference.None,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }
            );

            // Seed default work schedules
            var schedule1Id = Guid.NewGuid();
            var schedule2Id = Guid.NewGuid();

            modelBuilder.Entity<WorkSchedule>().HasData(
                new WorkSchedule
                {
                    Id = schedule1Id,
                    ScheduleName = "Standard Work Week",
                    SelectedDays = "1,2,3,4,5", // Monday to Friday
                    TimeRanges = "09:00:00-17:00:00", // 9 AM to 5 PM
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new WorkSchedule
                {
                    Id = schedule2Id,
                    ScheduleName = "Part Time",
                    SelectedDays = "1,3,5", // Monday, Wednesday, Friday
                    TimeRanges = "10:00:00-14:00:00", // 10 AM to 2 PM
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }
            );

            // Seed sample employees
            modelBuilder.Entity<Employee>().HasData(
                new Employee
                {
                    Id = Guid.NewGuid(),
                    Name = "Alice",
                    Surname = "Johnson",
                    Position = "Developer",
                    SupervisorId = supervisor1Id,
                    WorkScheduleId = schedule1Id,
                    Access = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Employee
                {
                    Id = Guid.NewGuid(),
                    Name = "Bob",
                    Surname = "Smith",
                    Position = "Tester",
                    SupervisorId = supervisor1Id,
                    WorkScheduleId = schedule2Id,
                    Access = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }
            );
        }
    }
}