resource "azurerm_network_security_rule" "noncompliant" {
  priority                    = 100
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "22"
  source_address_prefix       = "*"  # Noncompliant S6321
  destination_address_prefix  = "*"
}
