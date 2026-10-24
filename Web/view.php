<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ThanhDV // Package Manager</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inconsolata:wght@400;700&display=swap" rel="stylesheet">
    <style>
        :root{--dark-bg:#0a0a1a;--main-font-color:#00ff41;--secondary-color:#cccccc;--warning-color:#ff9900;--glitch-text:"ThanhDV // Package Manager"}
        body,html{height:100vh;margin:0;padding:0;font-family:'Inconsolata',monospace;background-color:var(--dark-bg);color:var(--main-font-color);overflow:hidden;}
        body::after{content:"";position:absolute;top:0;left:0;width:100%;height:100%;background:repeating-linear-gradient(0deg,rgba(0,0,0,.5),rgba(0,0,0,.5) 1px,transparent 1px,transparent 2px);pointer-events:none;z-index:10}
        .container{width:100%;height:100vh;position:relative;box-sizing:border-box;overflow-y:auto;}
        .content-wrapper {padding: 25px 40px;}
        .prompt{color:var(--secondary-color);font-size:1.2em}
        .prompt span, .prompt a{color:var(--main-font-color);text-decoration:none;border-bottom:1px dashed var(--main-font-color);cursor:pointer}
        .prompt a:hover{background-color:var(--main-font-color);color:var(--dark-bg)}
        .header-glitch{font-size:6em;font-weight:700;color:var(--main-font-color);position:relative;text-shadow:0 0 8px var(--main-font-color);margin:0;padding:0;animation:text-flicker .05s infinite alternate}
        .header-glitch::before,.header-glitch::after{content:var(--glitch-text);position:absolute;top:0;left:0;width:100%;height:100%;background:var(--dark-bg);overflow:hidden;clip-path:inset(50% 0 50% 0)}
        .header-glitch::before{left:-1px;text-shadow:-1px 0 red;animation:glitch-anim-1 2s infinite linear alternate-reverse}
        .header-glitch::after{left:1px;text-shadow:1px 0 #00f;animation:glitch-anim-2 3s infinite linear alternate-reverse}
        p{font-size:1.2em;color:var(--secondary-color);margin-top:20px;margin-bottom:30px}
        table{width:100%;border-collapse:collapse;color:var(--secondary-color);margin-top:20px;font-size:1.1em}
        th{text-align:left;color:var(--main-font-color);padding:12px}
        th,td{padding:12px;text-align:left;border-bottom:1px solid #333;vertical-align:top; word-wrap: break-word;}
        td a{color:var(--main-font-color);text-decoration:none}
        td a:hover{text-decoration:underline}
        .status-managed{color:#00ff41;font-weight:700}
        .status-unmanaged{color:var(--warning-color);font-weight:700}
        .action-btn{background:none;border:1px solid var(--warning-color);color:var(--warning-color);cursor:pointer;padding:4px 8px;font-family:inherit;transition:all .2s ease}
        .action-btn:hover{background-color:var(--warning-color);color:var(--dark-bg)}
        [contenteditable]{outline:none;border-bottom:1px solid var(--main-font-color);cursor:text;min-width:10px;display:inline-block}
        .pagination{margin-top:30px;display:flex;justify-content:space-between;align-items:center}
        .pagination a{color:var(--main-font-color);border:1px solid var(--main-font-color);padding:5px 10px;text-decoration:none}
        .pagination a:hover{background-color:var(--main-font-color);color:var(--dark-bg)}
        .pagination span.disabled{color:var(--secondary-color);border:1px solid var(--secondary-color);padding:5px 10px;opacity:0.5}
        #login-status a {border: 1px solid var(--secondary-color); padding: 5px 10px; text-decoration: none; transition: all 0.2s ease;}
        #login-status a:hover {background-color: var(--secondary-color); color: var(--dark-bg);}
        @keyframes text-flicker{from{text-shadow:0 0 8px var(--main-font-color)}to{text-shadow:0 0 5px var(--main-font-color)}}@keyframes glitch-anim-1{0%{clip-path:inset(30% 0 60% 0)}100%{clip-path:inset(40% 0 50% 0)}}@keyframes glitch-anim-2{0%{clip-path:inset(75% 0 5% 0)}100%{clip-path:inset(0 0 95% 0)}}
        #modal-overlay{display:none;position:fixed;top:0;left:0;width:100%;height:100%;background-color:rgba(0,0,0,.7);z-index:1000}
        .form-section{display:none;position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);width:90%;max-width:600px;background-color:#1a1a2a;padding:30px;border:1px solid var(--main-font-color);border-radius:5px;z-index:1001}
        .form-section h2{margin-top:0;color:var(--main-font-color);font-size:2em}
        .form-group{margin-bottom:15px}
        .form-group label{display:block;margin-bottom:5px;color:var(--secondary-color)}
        .form-group input, .form-group textarea{width:100%;background-color:#0a0a1a;border:1px solid var(--secondary-color);color:var(--main-font-color);padding:10px;font-family:inherit;box-sizing:border-box;border-radius:3px}
        .form-group input:focus, .form-group textarea:focus{border-color:var(--main-font-color);outline:none}
        .form-submit{background-color:var(--main-font-color);color:var(--dark-bg);border:none;padding:12px 20px;font-weight:700;cursor:pointer;font-family:inherit;font-size:1.1em;transition:all .2s ease}
        .form-submit:hover{background-color:#fff}
        .close-btn{position:absolute;top:10px;right:20px;color:var(--secondary-color);font-size:30px;font-weight:700;cursor:pointer}
        .close-btn:hover{color:#fff}
        .container { scrollbar-width: thin; scrollbar-color: var(--main-font-color) var(--dark-bg); }
        .container::-webkit-scrollbar { width: 12px; }
        .container::-webkit-scrollbar-track { background: var(--dark-bg); }
        .container::-webkit-scrollbar-thumb { background-color: var(--main-font-color); border-radius: 6px; border: 3px solid var(--dark-bg); }
        @media (max-width: 768px) {
            .header-glitch { font-size: 3em; --glitch-text: "ThanhDV"; }
            .content-wrapper { padding: 20px 15px; }
            table { font-size: 1em; }
            table, thead, tbody, th, td, tr { display: block; }
            thead tr { position: absolute; top: -9999px; left: -9999px; }
            tr { border: 1px solid #333; margin-bottom: 1em; }
            td { border: none; border-bottom: 1px solid #222; position: relative; padding-left: 50%; text-align: right; }
            td:before { content: attr(data-label); position: absolute; top: 12px; left: 12px; width: 45%; padding-right: 10px; white-space: nowrap; text-align: left; font-weight: bold; color: var(--main-font-color); }
            .form-section { width: 95%; padding: 20px; }
        }
        @media (min-width: 769px) {
            .file-list {
                table-layout: fixed; 
            }
            .file-list th:nth-child(1), .file-list td:nth-child(1) { width: 25%; }
            .file-list th:nth-child(2), .file-list td:nth-child(2) { width: 10%; }
            .file-list th:nth-child(3), .file-list td:nth-child(3) { width: 35%; }
            .file-list th:nth-child(4), .file-list td:nth-child(4) { width: 15%; }
            .file-list th:nth-child(5), .file-list td:nth-child(5) { width: 15%; text-align: center; }
        }
        .action-btn, .action-icon {
		    background: none;
		    border: none;
		    color: var(--secondary-color);
		    cursor: pointer;
		    padding: 0 8px;
		    font-size: 1.5em;
		    vertical-align: middle;
		    transition: all 0.2s ease;
		    display: inline-block;
		}
		.action-btn:hover, .action-icon:hover {
		    color: var(--main-font-color);
		    transform: scale(1.2);
		}
		.action-btn { 
		    color: var(--warning-color);
		}
    </style>
</head>
<body>
    <div class="container">
        <div class="content-wrapper">
            <div style="display: flex; justify-content: space-between; align-items: flex-start;">
                <div class="header-glitch">ThanhDV // Package Manager</div>
                <div id="login-status" class="prompt">
                    <?php if ($is_loggedin): ?>
                        <span>Welcome!</span> <a href="logout.php">Logout</a>
                    <?php else: ?>
                        <a href="login.php">Login</a>
                    <?php endif; ?>
                </div>
            </div>

            <p class="prompt">user@host:~$ <span>ls unitypkgs/ | grep -i '</span><span id="search-term" contenteditable="true"><?php echo htmlspecialchars($search_query ?: '*'); ?></span><span>'</span></p>
            <p class="prompt">TOTAL FILES FOUND: <span><?php echo $total_items; ?></span></p>
            
            <table class="file-list">
				<thead>
				    <tr>
				        <th>Package Name</th>
				        <th>Version</th>
				        <th>Description</th>
				        <?php if ($is_loggedin): ?><th>Status</th><?php endif; ?>
				        <th>Actions</th>
				    </tr>
				</thead>
                <tbody>
                    <?php
                    if (empty($files_list)) {
                        echo '<tr><td colspan="'.($is_loggedin ? '5':'4').'">No files found matching your query.</td></tr>';
                    } else {
                        foreach ($files_list as $file) {
							echo '<tr>';
							    echo '<td data-label="Package Name">' . htmlspecialchars(truncate_string($file['displayName'] ?: $file['name'], 50)) . '</td>';
							    echo '<td data-label="Version">' . htmlspecialchars($file['version'] ?: 'N/A') . '</td>';
							    echo '<td data-label="Description">' . htmlspecialchars(truncate_string($file['description'] ?: 'N/A', 100)) . '</td>';
							    
                                if ($is_loggedin) {
                                    if ($file['source'] === 'remote') {
                                        echo '<td data-label="Status"><span class="status-managed">Verdaccio</span></td>';
                                    } elseif ($file['managed']) {
                                        echo '<td data-label="Status"><span class="status-managed">UnityPkgs</span></td>';
                                    } else {
                                        echo '<td data-label="Status"><span class="status-unmanaged">Unmanaged</span></td>';
                                    }
                                }
							
                                $iconDownload = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="icon icon-tabler icons-tabler-outline icon-tabler-download"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2v-2" /><path d="M7 11l5 5l5 -5" /><path d="M12 4l0 12" /></svg>';
                                $iconEdit = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="icon icon-tabler icons-tabler-outline icon-tabler-edit"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M7 7h-1a2 2 0 0 0 -2 2v9a2 2 0 0 0 2 2h9a2 2 0 0 0 2 -2v-1" /><path d="M20.385 6.585a2.1 2.1 0 0 0 -2.97 -2.97l-8.415 8.385v3h3l8.385 -8.415z" /><path d="M16 5l3 3" /></svg>';
                                $iconAdd = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="icon icon-tabler icons-tabler-outline icon-tabler-square-rounded-plus"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M12 3c7.2 0 9 1.8 9 9s-1.8 9 -9 9s-9 -1.8 -9 -9s1.8 -9 9 -9z" /><path d="M15 12h-6" /><path d="M12 9v6" /></svg>';

                                echo '<td data-label="Actions">';
                                    echo '<a class="action-icon" href="' . htmlspecialchars($file['path']) . '" download title="Download ' . htmlspecialchars($file['name']) . '">' . $iconDownload . '</a>';
                                    if ($is_loggedin && $file['source'] === 'local') {
                                        if ($file['managed']) {
                                            echo '<button class="action-icon" data-filename="' . htmlspecialchars($file['name']) . '" title="Edit Info">' . $iconEdit . '</button>';
                                        } else {
                                            echo '<button class="action-icon" data-filename="' . htmlspecialchars($file['name']) . '" title="Add Info">' . $iconAdd . '</button>';
                                        }
                                    }
                                echo '</td>';
							
							echo '</tr>';
                        }
                    }
                    ?>
                </tbody>
            </table>

            <div class="prompt pagination">
                <?php if ($total_pages > 1): ?>
                    <div>
                        <?php if ($current_page > 1): ?>
                            <a href="?page=<?php echo $current_page - 1; ?><?php echo $search_param; ?>">&lt;&lt; Previous</a>
                        <?php else: ?>
                            <span class="disabled">&lt;&lt; Previous</span>
                        <?php endif; ?>
                    </div>
                    <div>
                        <span>Page <?php echo $current_page; ?> of <?php echo $total_pages; ?></span>
                    </div>
                    <div>
                        <?php if ($current_page < $total_pages): ?>
                            <a href="?page=<?php echo $current_page + 1; ?><?php echo $search_param; ?>">Next &gt;&gt;</a>
                        <?php else: ?>
                            <span class="disabled">Next &gt;&gt;</span>
                        <?php endif; ?>
                    </div>
                <?php else: ?>
                    <span>user@host:~$ <span>_</span></span>
                <?php endif; ?>
            </div>
        </div>
    </div>

    <?php if ($is_loggedin): ?>
    <div id="modal-overlay"></div>
    <div id="form-container" class="form-section">
        <span id="close-modal" class="close-btn">&times;</span>
        <h2>Add / Update Package Info</h2>
        <form action="index.php" method="POST">
            <div class="form-group">
                <label for="packageId">Package ID (e.g., com.company.package-name)</label>
                <input type="text" id="packageId" name="packageId" required>
            </div>
            <div class="form-group">
                <label for="displayName">Display Name</label>
                <input type="text" id="displayName" name="displayName" required>
            </div>
            <div class="form-group">
                <label for="version">Version</label>
                <input type="text" id="version" name="version" required>
            </div>
            <div class="form-group">
                <label for="url">URL</label>
                <input type="text" id="url" name="url" required readonly style="background-color: #333; cursor: not-allowed;">
            </div>
            <div class="form-group">
                <label for="description">Description</label>
                <textarea id="description" name="description" rows="3"></textarea>
            </div>
            <button type="submit" class="form-submit">Save Package Info</button>
        </form>
    </div>
    <?php endif; ?>

    <script>
        const isLoggedIn = <?php echo json_encode($is_loggedin); ?>;
        
        if (isLoggedIn) {
            const allPackageMeta = <?php echo json_encode($packages_by_filename, JSON_HEX_TAG); ?> || {};
            const formContainer = document.getElementById('form-container');
            const modalOverlay = document.getElementById('modal-overlay');
            const closeModalBtn = document.getElementById('close-modal');

            function openModal() { modalOverlay.style.display = 'block'; formContainer.style.display = 'block'; }
            function closeModal() { modalOverlay.style.display = 'none'; formContainer.style.display = 'none'; }

            document.querySelectorAll('.action-btn').forEach(button => {
                button.addEventListener('click', () => {
                    const filename = button.dataset.filename;
                    const meta = allPackageMeta[filename] || { id: '', displayName: '', version: '', description: '', url: '' };
                    
                    const fileRelativePath = 'unitypkgs/' + filename;
                    const fullUrl = new URL(fileRelativePath, window.location.href).href;

                    document.getElementById('packageId').value = meta.id;
                    document.getElementById('displayName').value = meta.displayName;
                    document.getElementById('version').value = meta.version;
                    document.getElementById('description').value = meta.description;
                    document.getElementById('url').value = meta.url || fullUrl;
                    
                    openModal();
                    document.getElementById('packageId').focus();
                });
            });
            
            closeModalBtn.addEventListener('click', closeModal);
            modalOverlay.addEventListener('click', closeModal);
            document.addEventListener('keydown', (event) => { if (event.key === 'Escape') { closeModal(); } });
        }
        
        const searchTermElement = document.getElementById('search-term');
        searchTermElement.addEventListener('keydown', function(event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                let query = searchTermElement.innerText.trim();
                window.location.href = 'index.php?search=' + encodeURIComponent(query);
            }
        });
    </script>
</body>
</html>