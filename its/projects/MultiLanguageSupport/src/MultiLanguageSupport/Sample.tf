resource "aws_apigatewayv2_domain_name" "example" {
  domain_name = "api.example.com"
  domain_name_configuration {} # S4426
}
