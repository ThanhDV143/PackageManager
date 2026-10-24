<?php
session_start();

if (isset($_SESSION['loggedin']) && $_SESSION['loggedin'] === true) {
    header('Location: index.php');
    exit;
}

$error_message = '';

$admin_user = 'thanhdv';
$admin_pass = 'Thanh=1998';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $username = $_POST['username'] ?? '';
    $password = $_POST['password'] ?? '';

    if ($username === $admin_user && $password === $admin_pass) {
        $_SESSION['loggedin'] = true;
        header('Location: index.php');
        exit;
    } else {
        $error_message = 'Invalid username or password.';
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Admin Login</title>
    <link href="https://fonts.googleapis.com/css2?family=Inconsolata:wght@400;700&display=swap" rel="stylesheet">
    <style>
        :root{--dark-bg:#0a0a1a;--main-font-color:#00ff41;--secondary-color:#cccccc;}
        body{background-color:var(--dark-bg);color:var(--secondary-color);font-family:'Inconsolata',monospace;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;}
        .login-box{background-color:#1a1a2a;padding:40px;border:1px solid var(--main-font-color);width:100%;max-width:320px;text-align:center;box-sizing:border-box;}
        h2{color:var(--main-font-color);margin-top:0;}
        input{width:100%;padding:10px;margin-bottom:15px;background-color:#0a0a1a;border:1px solid var(--secondary-color);color:var(--main-font-color);font-family:inherit;box-sizing:border-box;}
        button{background-color:var(--main-font-color);color:var(--dark-bg);border:none;padding:10px 15px;width:100%;font-weight:700;cursor:pointer;font-family:inherit;font-size:1.1em;}
        .error{color:red;margin-top:15px;height:20px;}
    </style>
</head>
<body>
    <div class="login-box">
        <h2>Admin Login</h2>
        <form method="POST" action="login.php">
            <input type="text" name="username" placeholder="Username" required>
            <input type="password" name="password" placeholder="Password" required>
            <button type="submit">Login</button>
            <?php if ($error_message): ?>
                <p class="error"><?php echo $error_message; ?></p>
            <?php endif; ?>
        </form>
    </div>
</body>
</html>