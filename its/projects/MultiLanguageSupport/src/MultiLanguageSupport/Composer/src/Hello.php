<?php
namespace Rivsen\Demo;

class Hello
{
    private $name;

    public function __construct( $name = 'World' )
    {
        $this->name = $name;
    }

    public function getName()
    {
        return $this->name;
    }

    public function hello()
    {
        return 'Hello '.$this->name.'!';
    }
}
