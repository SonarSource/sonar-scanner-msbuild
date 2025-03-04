<?php
require_once "vendor/autoload.php";

$hello = new Rivsen\Demo\Hello();
echo $hello->hello();

echo "\n";
$hiGirl = new Rivsen\Demo\Hello('My Goddess');
echo $hiGirl->hello();
