using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Models;

namespace FypMatchTracker.Data;

/// <summary>
/// Seeds the database on first run with:
/// - 7 ER diagram tables
/// - Default Departments
/// - Seeded Coordinator and Student users
/// - Default Similarity Settings (20%, 50%, 0.7, 0.3)
/// - Realistic NUML Department FYPs for immediate demonstration
/// </summary>
public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        // Automatically creates SQLite database file and tables if not exist
        context.Database.EnsureCreated();

        // Safe Schema Migration for existing SQLite databases
        try { context.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN University TEXT DEFAULT 'NUML Islamabad';"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE SimilarityChecks ADD COLUMN StudentName TEXT;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE SimilarityChecks ADD COLUMN UniversityName TEXT DEFAULT 'NUML Islamabad';"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE SimilarityChecks ADD COLUMN SupervisorName TEXT;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE FypProjects ADD COLUMN StudentName TEXT;"); } catch { }
        try { context.Database.ExecuteSqlRaw("ALTER TABLE FypProjects ADD COLUMN UniversityName TEXT DEFAULT 'NUML Islamabad';"); } catch { }
        try { context.Database.ExecuteSqlRaw("UPDATE FypProjects SET UniversityName = 'NUML Islamabad' WHERE UniversityName IS NULL;"); } catch { }
        try { context.Database.ExecuteSqlRaw("UPDATE SimilarityChecks SET StudentName = 'Kausar Khattak', UniversityName = 'NUML Islamabad' WHERE StudentName IS NULL OR StudentName = 'Guest';"); } catch { }

        // 1. Seed Departments
        if (!context.Departments.Any())
        {
            var depts = new List<Department>
            {
                new Department { Name = "Computer Science", Code = "CS" },
                new Department { Name = "Software Engineering", Code = "SE" },
                new Department { Name = "Artificial Intelligence", Code = "AI" },
                new Department { Name = "Information Technology", Code = "IT" }
            };
            context.Departments.AddRange(depts);
            context.SaveChanges();
        }

        // 2. Seed Similarity Setting (ER Table: SimilaritySetting)
        if (!context.SimilaritySettings.Any())
        {
            var setting = new SimilaritySetting
            {
                LowThreshold = 20.0,
                HighThreshold = 50.0,
                SemanticWeight = 0.70,
                KeywordWeight = 0.30,
                CrossrefApiEnabled = true,
                UpdatedAt = DateTime.UtcNow
            };
            context.SimilaritySettings.Add(setting);
            context.SaveChanges();
        }

        // 3. Seed Users (PasswordHasher ensures passwords are never stored in plain text)
        var hasher = new PasswordHasher<AppUser>();

        if (!context.Users.Any())
        {
            var coordinator = new AppUser
            {
                FullName = "Kausar Hayat",
                Email = "coordinator@numl.edu.pk",
                Role = "Coordinator",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            coordinator.PasswordHash = hasher.HashPassword(coordinator, "Admin@123");

            var student = new AppUser
            {
                FullName = "Kausar Khattak",
                Email = "s2258077@numl.edu.pk",
                RollNumber = "2258077",
                Role = "Student",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            student.PasswordHash = hasher.HashPassword(student, "Student@123");

            context.Users.AddRange(coordinator, student);
            context.SaveChanges();
        }
        else
        {
            // Sync coordinator name if it was previously seeded
            var existingCoord = context.Users.FirstOrDefault(u => u.Email == "coordinator@numl.edu.pk");
            if (existingCoord != null && existingCoord.FullName != "Kausar Hayat")
            {
                existingCoord.FullName = "Kausar Hayat";
                context.SaveChanges();
            }
        }

        // 4. Seed Past NUML Projects (ER Table: FypProject)
        if (!context.FypProjects.Any())
        {
            var csDept = context.Departments.FirstOrDefault(d => d.Name == "Computer Science") 
                         ?? context.Departments.First();

            var pastProjects = new List<FypProject>
            {
                new FypProject
                {
                    Title = "Real-Time Face Recognition Attendance System with Anti-Spoofing",
                    Description = "An automated attendance capture system for lecture halls using OpenCV and Haar cascades with liveness detection to prevent photo spoofing.",
                    Keywords = "attendance, face recognition, real-time, opencv, python, computer vision",
                    Year = 2023,
                    Status = "Completed",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-14)
                },
                new FypProject
                {
                    Title = "Autonomous UAV Pathfinding using Edge YOLOv8 and ROS2",
                    Description = "A micro-aerial drone navigation framework running quantized YOLOv8 object detection on onboard edge computers for GPS-denied environments.",
                    Keywords = "drone, uav, navigation, yolo, yolov8, edge computing, ros2, path planning",
                    Year = 2024,
                    Status = "Approved",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6)
                },
                new FypProject
                {
                    Title = "Blockchain Credential Verification for University Transcripts & Degrees",
                    Description = "A decentralized smart contract ledger on Polygon network allowing employers and universities to instantly verify student degree authenticity.",
                    Keywords = "blockchain, smart contracts, credential verification, polygon, solidity, degree",
                    Year = 2026,
                    Status = "Reserved",
                    ReservationToken = "NUML-FYP-2026-9842",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new FypProject
                {
                    Title = "Deep Learning Agricultural Crop Disease Detection and Advisory Platform",
                    Description = "A mobile application utilizing convolutional neural networks (ResNet-50) to classify leaf diseases from camera photos and recommend pesticide treatments.",
                    Keywords = "deep learning, crop disease, agriculture, cnn, resnet, mobile app, classification",
                    Year = 2023,
                    Status = "Completed",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-18)
                },
                new FypProject
                {
                    Title = "Smart Hospital Patient Triage and Medical Appointment Scheduling System",
                    Description = "A cloud-based hospital management solution with intelligent priority queue allocation and automated SMS reminder dispatch for clinic appointments.",
                    Keywords = "hospital, appointment scheduling, patient triage, queue management, healthcare",
                    Year = 2022,
                    Status = "Completed",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-26)
                },
                new FypProject
                {
                    Title = "Cybersecurity Intrusion Detection System Using Random Forest and Network Telemetry",
                    Description = "A real-time network packet analyzer that flags anomalous traffic, DDoS attacks, and port scanning utilizing ensemble machine learning algorithms.",
                    Keywords = "cybersecurity, intrusion detection, ids, network traffic, random forest, ddos, anomaly",
                    Year = 2024,
                    Status = "Completed",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-8)
                },
                new FypProject
                {
                    Title = "Automated Online Exam Proctoring and Gaze Tracking System",
                    Description = "An AI-powered web proctoring software that monitors multiple student video streams to detect suspicious head movements, unauthorized devices, and background voices.",
                    Keywords = "exam proctoring, gaze tracking, eye tracking, computer vision, cheating detection, ai",
                    Year = 2023,
                    Status = "Completed",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-12)
                },
                new FypProject
                {
                    Title = "Intelligent Traffic Congestion Monitoring and Dynamic Signal Timing",
                    Description = "Urban traffic management system using intersection surveillance camera feeds to estimate vehicle density and dynamically adapt green light duration.",
                    Keywords = "traffic management, vehicle density, computer vision, smart signals, urban mobility",
                    Year = 2022,
                    Status = "Completed",
                    DepartmentId = csDept.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-28)
                }
            };

            context.FypProjects.AddRange(pastProjects);
            context.SaveChanges();
        }
    }
}
