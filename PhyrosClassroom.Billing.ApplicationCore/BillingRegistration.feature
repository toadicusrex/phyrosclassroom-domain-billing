Feature: Billing registration

Scenario: Registering a billing makes the read model available
	Given the Billing application composition is configured
	When I register a billing named "Alice" "Bennett"
	Then the registered billing can be retrieved from the query side
