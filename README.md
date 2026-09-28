# GameDev

A game development project created for learning and development.

##  Git & GitHub Commands

### 1. First-Time Setup

If you already created the GitHub repository, connect your local project to it:

```bash
git remote set-url origin https://github.com/jinvnce/GameDev.git
```

Check if the remote is correct:

```bash
git remote -v
```

You should see:

```text
origin  https://github.com/jinvnce/GameDev.git (fetch)
origin  https://github.com/jinvnce/GameDev.git (push)
```

---

## 2. Upload Your Project to GitHub

After making changes to your project:

### Step 1 — Check your files

```bash
git status
```

This shows which files were changed.

### Step 2 — Add your changes

```bash
git add .
```

The `.` means **add all changed files**.

### Step 3 — Create a commit

```bash
git commit -m "Describe your changes"
```

Example:

```bash
git commit -m "Added player movement"
```

### Step 4 — Push to GitHub

```bash
git push
```

For the first push of `main`, use:

```bash
git push -u origin main
```

After that, you can normally use:

```bash
git push
```

---

#  Working on Another Computer

If you use the project on another PC or laptop, always get the latest version first.

### Download the latest changes

```bash
git pull
```

Then work on your project.

After making changes:

```bash
git add .
git commit -m "Describe your changes"
git push
```

### Typical workflow

```text
GitHub
   ↑
   │ git push
   │
Your Computer
   │
   │ edit files
   │
   ↓
git add .
git commit
git push
```

When switching computers:

```text
Other Computer
      │
      │ git pull
      ↓
Gets latest version
      │
      ↓
Edit files
      │
      ↓
git add .
git commit
git push
```

---

#  Branches

Branches allow you to work on a feature without directly changing `main`.

### See your branches

```bash
git branch
```

The branch with `*` is your current branch.

Example:

```text
* main
  player-movement
  enemy-system
```

---

## Create a New Branch

```bash
git branch player-movement
```

Then switch to it:

```bash
git switch player-movement
```

Or do both at once:

```bash
git switch -c player-movement
```

Example:

```bash
git switch -c player-movement
```

Now you are working on:

```text
player-movement
```

instead of:

```text
main
```

---

## Push a New Branch

The first time you push a new branch:

```bash
git push -u origin player-movement
```

After that:

```bash
git push
```

The branch will also appear on GitHub.

---

#  Switching Between Branches

Go back to `main`:

```bash
git switch main
```

Go to your feature branch:

```bash
git switch player-movement
```

Check which branch you're currently using:

```bash
git branch
```

---

# 🔗 Merge a Branch

When your feature is finished, you can merge it into `main`.

First switch to `main`:

```bash
git switch main
```

Get the latest version:

```bash
git pull
```

Merge your feature:

```bash
git merge player-movement
```

Then upload the merged version:

```bash
git push
```

Example:

```text
player-movement
       │
       │ git merge
       ↓
      main
       │
       │ git push
       ↓
     GitHub
```

---

#  Delete a Branch

After a branch has been merged and you no longer need it:

### Delete local branch

```bash
git branch -d player-movement
```

### Delete GitHub branch

```bash
git push origin --delete player-movement
```

Only delete a branch when you're sure you don't need it anymore.

---

#  Useful Commands

### Check current status

```bash
git status
```

### See commit history

```bash
git log
```

Short version:

```bash
git log --oneline
```

### See all branches

```bash
git branch
```

### See remote repository

```bash
git remote -v
```

### Download changes

```bash
git pull
```

### Upload changes

```bash
git push
```

### Add all changes

```bash
git add .
```

### Commit changes

```bash
git commit -m "Your message"
```

---

#  Important: `git add` vs `git remote`

These commands do completely different things.

### `git add`

Used for files:

```bash
git add .
```

Meaning:

> "Prepare my changed files for the next commit."

### `git remote`

Used for your GitHub repository:

```bash
git remote set-url origin https://github.com/jinvnce/GameDev.git
```

Meaning:

> "Tell my local project which GitHub repository `origin` refers to."

---

#  Quick Daily Workflow

If you're already connected to GitHub:

```bash
git pull
git status
git add .
git commit -m "Describe changes"
git push
```

That's the main workflow to remember.

---

# 💻 Switching Between Laptop and PC

### Before working

```bash
git pull
```

### After working

```bash
git add .
git commit -m "Describe changes"
git push
```

This keeps both computers synchronized through GitHub.

---

## Example

### Laptop

```bash
git pull
# Make changes

git add .
git commit -m "Added player movement"
git push
```

### PC

```bash
git pull
# Latest player movement is downloaded

# Make more changes

git add .
git commit -m "Added enemy system"
git push
```

### Back to Laptop

```bash
git pull
```

Now the laptop receives the changes made on the PC.

---

# 🧠 Commands to Memorize First

You don't need to memorize everything immediately.

Start with these:

```bash
git status
git pull
git add .
git commit -m "message"
git push
```

Once you're comfortable with those, learn:

```bash
git branch
git switch
git merge
```
