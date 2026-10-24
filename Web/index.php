<?php
session_start();
require_once 'logic.php';

$is_loggedin = isset($_SESSION['loggedin']) && $_SESSION['loggedin'] === true;

$search_query = isset($_GET['search']) ? trim($_GET['search']) : '';
$packages_json_path = __DIR__ . '/packages.json';

if ($is_loggedin && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $packageId = filter_input(INPUT_POST, 'packageId', FILTER_SANITIZE_STRING);
    $displayName = filter_input(INPUT_POST, 'displayName', FILTER_SANITIZE_STRING);
    $version = filter_input(INPUT_POST, 'version', FILTER_SANITIZE_STRING);
    $description = filter_input(INPUT_POST, 'description', FILTER_SANITIZE_STRING);
    $url = filter_input(INPUT_POST, 'url', FILTER_SANITIZE_URL);

    if (!empty($packageId) && !empty($url)) {
        $packages = file_exists($packages_json_path) ? json_decode(file_get_contents($packages_json_path), true) : [];
        if (!is_array($packages)) { $packages = []; }
        
        $packages[$packageId] = [
            'displayName' => $displayName,
            'version' => $version,
            'description' => $description,
            'url' => $url,
            'lastUpdated' => date('Y-m-d H:i:s')
        ];
        
        file_put_contents($packages_json_path, json_encode($packages, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
        
        header("Location: " . $_SERVER['PHP_SELF']);
        exit;
    }
}

$package_data = file_exists($packages_json_path) ? json_decode(file_get_contents($packages_json_path), true) : [];
$verdaccio_url = 'https://upm.thanhdv.com/-/verdaccio/data/packages';
$verdaccio_json = @file_get_contents($verdaccio_url);
$verdaccio_packages = $verdaccio_json ? json_decode($verdaccio_json, true) : [];

if (!is_array($verdaccio_packages)) { $verdaccio_packages = []; }
if (!is_array($package_data)) { $package_data = []; }

$packages_by_filename = [];
foreach ($package_data as $pkgId => $pkg) {
    if (isset($pkg['url'])) {
        $filename_from_url = basename(parse_url($pkg['url'], PHP_URL_PATH));
        $decoded_filename = urldecode($filename_from_url);
        $packages_by_filename[$decoded_filename] = array_merge($pkg, ['id' => $pkgId]);
    }
}

$directory = 'unitypkgs/';
$local_files = findUnityPackagesRecursive(__DIR__ . '/' . $directory, $directory, $search_query, $packages_by_filename);
$all_files = merge_packages($local_files, $verdaccio_packages);

usort($all_files, function($a, $b) {
    if ($a['managed'] !== $b['managed']) {
        return $a['managed'] ? -1 : 1;
    }
    return strcasecmp($a['name'], $b['name']);
});

$total_items = count($all_files);
$items_per_page = 20;
$total_pages = ceil($total_items / $items_per_page);
$current_page = isset($_GET['page']) && is_numeric($_GET['page']) ? (int)$_GET['page'] : 1;
if ($current_page < 1 || ($current_page > $total_pages && $total_pages > 0)) { $current_page = 1; }
$offset = ($current_page - 1) * $items_per_page;
$files_list = array_slice($all_files, $offset, $items_per_page);
$search_param = !empty($search_query) ? '&search=' . urlencode($search_query) : '';

// Gọi file view để hiển thị
require_once 'view.php';
?>