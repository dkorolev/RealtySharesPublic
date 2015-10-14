<?php

// To start the server, tested on PHP 5.5.9.
// $ php -S localhost:8000 server.php
//
// To test it from bash:
// $ curl -s -w "\n%{http_code}\n" -d '{"a":1,"b":42}' localhost:8000/add
//
// The rest is F#.

define("LOG_FILENAME", "php-log.txt");

function appendToLog($ref, $s) {
  $f = fopen(LOG_FILENAME, "a") or die("Cannot open '" . LOG_FILENAME . "' for appending.\n");
  fwrite($f, $ref . date(" @ Y/m/d, H:i:s :: ") . $s . "\n");
  fclose($f);
};

$ref = "rs-" . date("YmdHis") . "-" . rand(1000, 9999);
appendToLog($ref, "Starting request.");

http_response_code(500);
header('Content-type: application/json');

if ($_SERVER["REQUEST_METHOD"] != "POST") {
  http_response_code(405);
  echo json_encode(array("error" => "Need POST request.", "reference_code" => $ref));
  appendToLog($ref, "Need POST.");
  return true;
} else {
  $command = $_SERVER["PHP_SELF"];
  $body = json_decode(file_get_contents('php://input'));
  if (!is_object($body)) {
    http_response_code(400);
    echo json_encode(array("error" => "Need JSON body.", "reference_code" => $ref));
    appendToLog($ref, "Need JSON body.");
    return true;
  } else {
    if ($command == "/add") {
      http_response_code(200);
      appendToLog($ref, "Adding " . $body->a . " and " . $body->b);
      echo json_encode(array("sum" => $body->a + $body->b, "reference_code" => $ref));
      appendToLog($ref, "Done.");
      return true;
    } else {
      http_response_code(404);
      echo json_encode(array("error" => "Not found.", "reference_code" => $ref));
      appendToLog($ref, "Route not found.");
      return true;
    }
  }
}

?>
