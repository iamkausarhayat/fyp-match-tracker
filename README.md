# NUML FYP Match Tracker & Duplicate Prevention System

An intelligent, web-based Final Year Project (FYP) management and originality verification system designed for university departments. Powered by **ASP.NET Core (.NET 10)**, **Entity Framework Core**, **SQLite**, and **Local ONNX Semantic AI**.

---

## 🚀 Quick Start (Run Locally)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 1-Click Run on Windows
Simply double-click:
```
run.bat
```
The browser will automatically open at `http://localhost:5242`.

### Manual Run (Terminal / Command Prompt)
```bash
git clone <your-repository-url>
cd FYP
dotnet run
```
Then open `http://localhost:5242` in your web browser.

---

## 👥 Default Demo Accounts

| Role | Email | Password | Purpose |
| :--- | :--- | :--- | :--- |
| **Student** | `s2258077@numl.edu.pk` | `Student@123` | Proposal check, originality report, reservation |
| **Coordinator** | `coordinator@numl.edu.pk` | `Admin@123` | Department dashboard, library archive, approvals |
| **Guest Mode** | *(No credentials needed)* | *(1-click)* | Instant proposal check without registration |

---

## 🌐 24/7 Free Cloud Deployment (Render.com)

You can host this application online 24/7 for **100% FREE** using Render and Docker:

1. **Push this repository to your GitHub account**:
   ```bash
   git init
   git add .
   git commit -m "Initial commit - FYP Match Tracker"
   git branch -M main
   git remote add origin https://github.com/<your-username>/<your-repo-name>.git
   git push -u origin main
   ```
2. **Go to [Render.com](https://render.com/)** and sign up for a free account (using GitHub).
3. Click **New +** -> **Web Service**.
4. Select your GitHub repository.
5. In the settings:
   - **Environment**: `Docker` (Render will automatically detect the `Dockerfile`).
   - **Instance Type**: `Free` ($0/month).
6. Click **Create Web Service**.
7. Render will build the container and provide you with a permanent public live URL (e.g. `https://fyp-match-tracker.onrender.com`) that works 24/7 on any mobile phone, tablet, or laptop worldwide!

---

## 🛠️ Architecture & Tech Stack

- **Backend**: C# (.NET 10 ASP.NET Core MVC)
- **Database**: SQLite with Entity Framework Core (Code-First)
- **AI / Embeddings**: Local ONNX Runtime (`all-MiniLM-L6-v2`) with Cosine Similarity (70%)
- **Keyword Matcher**: Jaccard Similarity Index with Stop-Words Filter (30%)
- **External Literature**: Crossref Public REST API client
- **Document Parser**: Multi-format PDF (PdfPig), Word .docx (OpenXML), and Text files
- **Frontend**: Bootstrap 5, Bootstrap Icons, Vanilla CSS & Modern Responsive Design

---

## 📄 License
Academic & Departmental Use Only &bull; National University of Modern Languages (NUML)
