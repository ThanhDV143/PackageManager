<?php
function formatBytes($bytes, $precision = 2) {
    $units = ['B', 'KB', 'MB', 'GB', 'TB'];
    $bytes = max($bytes, 0);
    $pow = floor(($bytes ? log($bytes) : 0) / log(1024));
    $pow = min($pow, count($units) - 1);
    if (pow(1024, $pow) > 0) { $bytes /= pow(1024, $pow); }
    return round($bytes, $precision) . ' ' . $units[$pow];
}

function truncate_string($string, $maxLength) {
    if (mb_strlen($string) > $maxLength) {
        return mb_substr($string, 0, $maxLength) . '...';
    }
    return $string;
}

function findUnityPackagesRecursive($startDir, $relativeBaseDir, $query, $packages_by_filename) {
    $files = [];
    if (!is_dir($startDir)) { return $files; }
    $allowed_extensions = ['unitypackage', 'rar', 'zip', '7z', 'tar', 'gz', 'tgz'];
    $iterator = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($startDir, RecursiveDirectoryIterator::SKIP_DOTS), RecursiveIteratorIterator::SELF_FIRST);

    foreach ($iterator as $fileInfo) {
        $extension = strtolower($fileInfo->getExtension());
        if ($fileInfo->isFile() && in_array($extension, $allowed_extensions)) {
            $filename = $fileInfo->getFilename();
            if (empty($query) || stripos($filename, $query) !== false) {
                $subPath = $iterator->getSubPathName();
                $normalizedSubPath = str_replace('\\', '/', $subPath);
                
                $is_managed = isset($packages_by_filename[$filename]);
                $meta = $is_managed ? $packages_by_filename[$filename] : [];

                $files[] = [
                    'name' => $filename,
                    'path' => rtrim($relativeBaseDir, '/') . '/' . $normalizedSubPath,
                    'size' => $fileInfo->getSize(),
                    'time' => $fileInfo->getMTime(),
                    'managed' => $is_managed,
                    'id' => $meta['id'] ?? '',
                    'displayName' => $meta['displayName'] ?? '',
                    'version' => $meta['version'] ?? '',
                    'description' => $meta['description'] ?? '',
                    'url' => $meta['url'] ?? '',
                ];
            }
        }
    }
    return $files;
}

function merge_packages($local_files, $remote_packages) {
    $unified_list = [];

    foreach ($local_files as $file) {
        $unified_list[$file['name']] = array_merge($file, ['source' => 'local']);
    }

    foreach ($remote_packages as $pkg) {
        $filename = basename(parse_url($pkg['dist']['tarball'], PHP_URL_PATH));
        
        if (!isset($unified_list[$filename])) {
            $unified_list[$filename] = [
                'name' => $filename,
                'path' => $pkg['dist']['tarball'],
                'size' => 0,
                'time' => strtotime($pkg['time']),
                'managed' => true,
                'id' => $pkg['name'],
                'displayName' => $pkg['displayName'] ?? $pkg['name'],
                'version' => $pkg['version'],
                'description' => $pkg['description'] ?? '',
                'url' => $pkg['dist']['tarball'],
                'source' => 'remote',
            ];
        }
    }
    
    return array_values($unified_list);
}
?>