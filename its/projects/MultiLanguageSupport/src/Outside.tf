provider "aws" {
  region = "us-east-1"
}

# TODO: Some todo <- S1135
resource "aws_s3_bucket" "hello_world_bucket" {
  bucket = "my-unique-hello-world-bucket-12345"
  acl    = "private"
}
